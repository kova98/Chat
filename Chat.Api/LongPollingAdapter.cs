using System.Net;
using System.Text.Json;

namespace Chat.Api;

public class LongPollingAdapter(MessagingService service, LongPollingConnectionRepository repo) : IConnectionAdapter
{
    private const int TimeoutInSeconds = 30;
    
    private HttpContext _context;
    private string _name;
    
    public async Task HandleLongPollingRequest(HttpContext ctx, CancellationToken ct, string name, string? idString)
    {
        _ = Guid.TryParse(idString, out var id);
        
        _name = name;
        _context = ctx;
        
        var connectionExists = repo.Connections.TryGetValue(name, out var existingConnection);
        if (connectionExists && existingConnection!.Id != id)
        {
            // New user, same name
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync($"Name '{name}' already taken", ct);
            return;
        }
        
        if (!connectionExists)
        {
            // Completely new user
            var error = await service.TryAddUser(this, name);
            if (error != null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync(error, ct);
                return;
            }

            id = Guid.NewGuid();
            var connection = new LongPollingConnection(this, DateTimeOffset.UtcNow, id);
            repo.Connections.TryAdd(name, connection);
        }
        // Same user, same name, retry after timeout

        ctx.Response.Headers.Add("X-Connection-Id", id.ToString());
        
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(TimeoutInSeconds));
        
        while (!cts.IsCancellationRequested)
        {
            if (repo.Buffers.TryGetValue(name, out var buffer))
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                await JsonSerializer.SerializeAsync(ctx.Response.Body, buffer, cancellationToken: ct);
                repo.Buffers.TryRemove(name, out _);
                return;
            }
            
            // keep alive
            repo.Connections[name].LastSeen = DateTimeOffset.UtcNow;
            
            // smaller delay makes server more responsive, but decreases performance
            await Task.Delay(50);
        }
        
        ctx.Response.StatusCode = (int)HttpStatusCode.NoContent;
    }

    public Task CloseConnection(string reason)
    {
        repo.Connections.Remove(_name, out _);
        return _context.Response.WriteAsync(reason);
    }

    public Task SendMessage(Message message)
    {
        // Could be further optimized by avoiding new list creation
        repo.Buffers.GetOrAdd(_name, _ => new List<Message> { message });
        return Task.CompletedTask;
    }
}

