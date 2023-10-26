using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.AspNetCore.Http.Features;

namespace Chat.Api;

public class WebTransportAdapter(MessagingService service) : IConnectionAdapter
{
    private PipeWriter outputPipe;
    
    public async Task CloseConnection(string reason)
    {
        await outputPipe.CompleteAsync(new SocketException((int)SocketError.ConnectionAborted));
    }

    public async Task SendMessage(Message message)
    {
        await JsonSerializer.SerializeAsync(outputPipe.AsStream(), message);
    }

    public async Task HandleWebTransportRequest(IWebTransportSession session, string name)
    {

        ConnectionContext? stream;
        IStreamDirectionFeature? direction;
        while (true)
        {
            // wait until we get a stream
            stream = await session.AcceptStreamAsync(CancellationToken.None);
            if (stream is null)
            {
                // if a stream is null, this means that the session failed to get the next one.
                // Thus, the session has ended, or some other issue has occurred. We end the
                // connection in this case.
                await service.RemoveUser(name);
                return;
            }

            // check that the stream is bidirectional. If yes, keep going, otherwise
            // dispose its resources and keep waiting.
            direction = stream.Features.GetRequiredFeature<IStreamDirectionFeature>();
            if (direction.CanRead && direction.CanWrite)
            {
                break;
            }

            await stream.DisposeAsync();
        }

        var inputPipe = stream!.Transport.Input;
        outputPipe = stream!.Transport.Output;

        await service.TryAddUser(this, name);
        
        var memory = new Memory<byte>(new byte[2 * 1024]);
        var length = await inputPipe.AsStream().ReadAsync(memory);
        var messageString = Encoding.UTF8.GetString(memory[..length].Span);
        var message = JsonSerializer.Deserialize<ChatMessage>(messageString);
        if (message == null)
        { 
            var error = $"Invalid message '{messageString}'";
            await outputPipe.CompleteAsync(new SocketException((int)SocketError.InvalidArgument));
            await outputPipe.WriteAsync(Encoding.UTF8.GetBytes(error));
            await stream.DisposeAsync();
            return;
        }

        await service.BroadcastMessage(message);
    }
}