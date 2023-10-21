using System.Net;
using System.Text.Json;

namespace Chat.Api;

public class LongPollingAdapter(MessagingService service, LongPollingConnectionRepository repo) : IConnectionAdapter
{
    private const int TimeoutInSeconds = 30;
    
    private HttpContext _context;
    private string _name;
    
    // TODO: solve memory allocation issues
    public async Task HandleLongPollingRequest(HttpContext ctx, CancellationToken ct, string name, string idString)
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
        
        // TODO: potential problem, check memory allocations
        while (!cts.IsCancellationRequested)
        {
            var buffer = repo.Buffers.GetOrAdd(name, _ => new List<Message>());
            if (buffer.Count > 0)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                var messageJson = JsonSerializer.Serialize(buffer);
                await ctx.Response.WriteAsync(messageJson, ct);
                buffer.Clear();
                return;
            }
            
            // keep alive
            repo.Connections[name].LastSeen = DateTimeOffset.UtcNow;
            await Task.Delay(500, ct);
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
        var buffer = repo.Buffers.GetOrAdd(_name, _ => new List<Message>());
        buffer.Add(message);
        
        return Task.CompletedTask;
    }
}

