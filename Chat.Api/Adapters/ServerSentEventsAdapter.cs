using System.Collections.Concurrent;
using System.Text.Json;

namespace Chat.Api.Adapters;

public class ServerSentEventsAdapter(MessagingService service) : IConnectionAdapter
{
    private readonly ConcurrentQueue<Message> _buffer = new();
    private CancellationTokenSource _cts;
    
    public async Task HandleServerSentEventsRequest(HttpContext context, CancellationToken ct, string name)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var error = await service.TryAddUser(this, name);
        if (error != null)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(error, ct);
            return;
        }
        
        context.Response.Headers.Add("Content-Type", "text/event-stream");
        
        while (!_cts.IsCancellationRequested)
        {
            if (_buffer.TryDequeue(out var message))
            {
                await context.Response.WriteAsync($"data: ", ct);
                await JsonSerializer.SerializeAsync(context.Response.Body, message, cancellationToken: ct);
                await context.Response.WriteAsync($"\n\n", ct);
                await context.Response.Body.FlushAsync(ct);
            }
        }
        
        await service.RemoveUser(name);
    }
    
    public Task CloseConnection(string reason)
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    public Task SendMessage(Message message)
    {
        _buffer.Enqueue(message);
        return Task.CompletedTask;
    }

}