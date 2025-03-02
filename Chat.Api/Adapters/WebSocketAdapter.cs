using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Chat.Api.Adapters;

public class WebSocketAdapter(MessagingService messagingService, ILogger<WebSocketAdapter> logger) : IConnectionAdapter
{
    private WebSocket Socket;
    
    public async Task SendMessage(Message message)
    {
        var messageString = JsonSerializer.Serialize(message);
        var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageString));
        await Socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    public async Task CloseConnection(string reason)
    {
        await Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, reason, default);
    }

    public async Task HandleUser(HttpContext context, string name)
    {
        Socket = await context.WebSockets.AcceptWebSocketAsync();
        
        var addUserError = await messagingService.TryAddUser(this, name);
        if (addUserError != null)
        {
            await Socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, addUserError, default);
            return;
        }

        try
        {
            await Receive(Socket, async (messageString) =>
            {
                var message = JsonSerializer.Deserialize<ChatMessage>(messageString);
                if (message == null)
                { 
                    var error = $"Invalid message '{messageString}'";
                    await Socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, error, default);
                    await messagingService.RemoveUser(name);
                    return;
                }
                        
                await messagingService.BroadcastMessage(message);
            });
            
            await messagingService.RemoveUser(name);
        }
        catch (WebSocketException e)
        {
            await messagingService.RemoveUser(name);
            logger.LogError(e, "WebSocket error");
        }
    }

    private async Task Receive(WebSocket socket, Func<string, Task> handleMessage)
    {
        var buffer = new byte[1024 * 2];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), default);
            if (result.EndOfMessage == false)
            {
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Max message size is 2KiB.", default);
                return;
            }
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await Socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, default);
                return;
            }
            if (result.MessageType != WebSocketMessageType.Text)
            {
                await Socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Expected text message", default);
                return;
            }
            var messageString = Encoding.UTF8.GetString(buffer[..result.Count]);

            await handleMessage(messageString);
        }
    }
}