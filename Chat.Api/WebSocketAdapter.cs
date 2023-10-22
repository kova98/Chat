using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Chat.Api;

public class WebSocketAdapter(MessagingService messagingService, ILogger<WebSocketAdapter> logger) : IConnectionAdapter
{
    private WebSocket Socket;

    public async Task CloseConnection(string reason)
    {
        await Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, reason, default);
    }

    public async Task HandleUser(HttpContext context, string name)
    {
        Socket = await context.WebSockets.AcceptWebSocketAsync();

        var error = await messagingService.TryAddUser(this, name);
        if (error != null)
        {
            await Socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, error, default);
            return;
        }

        try
        {
            await Receive(Socket, name, async (result, buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await messagingService.RemoveUser(name);
                    await Socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, default);
                    return;
                }
                
                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await Socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "Expected text message", default);
                    return;
                }
                
                var messageString = Encoding.UTF8.GetString(buffer);
                var message = JsonSerializer.Deserialize<ChatMessage>(messageString);
                if (message == null)
                { 
                    var error = $"Invalid message '{messageString}'";
                    await Socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, error, default);
                    await messagingService.RemoveUser(name);
                    return;
                }
                        
                var chatMessage = JsonSerializer.Deserialize<ChatMessage>(messageString);
                await messagingService.BroadcastMessage(chatMessage);
            });
        }
        catch (WebSocketException e)
        {
            await messagingService.RemoveUser(name);
            logger.LogError(e, "WebSocket error");
        }
    }

    private async Task Receive(WebSocket socket, string name,
        Action<WebSocketReceiveResult, ArraySegment<byte>> handleMessage)
    {
        var buffer = new byte[1024 * 2];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), default);
            if (result.EndOfMessage == false)
            {
                await messagingService.RemoveUser(name);
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Max message size is 2KiB.", default);
                return;
            }

            handleMessage(result, new ArraySegment<byte>(buffer, 0, result.Count));
        }
        
        // await messagingService.RemoveUser(name);
    }

    public async Task SendMessage(Message message)
    {
        // if (Socket.State != WebSocketState.Open)
        // {
        //     return;
        // }
        
        var messageString = JsonSerializer.Serialize(message);
        var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageString));
        await Socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
}