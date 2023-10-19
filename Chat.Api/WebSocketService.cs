using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Chat.Api;

public class WebSocketService(ILogger<WebSocketService> logger)
{
    private static readonly ConcurrentQueue<ChatMessage> History = new();
    private static readonly ConcurrentDictionary<string, WebSocket> Connections = new();
    
    public async Task HandleWebSocket(HttpContext context, string name)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        
        if (Connections.ContainsKey(name))
        {
            await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, $"Name '{name}' already taken" , default);
            return;
        }
        
        Connections.TryAdd(name, socket);

        
        await SendMessage(socket, new History(History.ToArray()));
        await BroadcastMessage(new UserList(Connections.Keys.ToArray()));

        try
        {
            await Receive(socket, async (result, buffer) =>
            {
                switch (result.MessageType)
                {
                    case WebSocketMessageType.Text:
                    {
                        var messageString = Encoding.UTF8.GetString(buffer);
                        var message = JsonSerializer.Deserialize<Message>(messageString);
                        switch (message.Type)
                        {
                            case "ChatMessage":
                                var chatMessage = JsonSerializer.Deserialize<ChatMessage>(messageString);
                                History.Enqueue(chatMessage);
                                await BroadcastMessage(chatMessage);
                                return;
                            default:
                                var error = $"Unknown message type '{message.Type}'";
                                await socket.CloseAsync(WebSocketCloseStatus.InvalidMessageType, error, default);
                                await RemoveUser(name);
                                return;
                        }
                    }
                    case WebSocketMessageType.Close:
                        await RemoveUser(name);
                        await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, default);
                        return;
                    case WebSocketMessageType.Binary:
                    default:
                        break;
                }
            });
        }
        catch (WebSocketException e)
        {
            await RemoveUser(name);
            logger.LogWarning("WebSocketException: {message}", e.Message);
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
        }
    }

    private async Task RemoveUser(string name)
    {
        logger.LogInformation("User '{name}' disconnected.", name);
        Connections.TryRemove(name, out _);
        await BroadcastMessage(new UserList(Connections.Keys.ToArray()));
    }

    private static async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, ArraySegment<byte>> handleMessage)
    {
        var buffer = new byte[1024 * 4];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), default);
            handleMessage(result, new ArraySegment<byte>(buffer, 0, result.Count));
        }
    }

    private async Task SendMessage(WebSocket socket, object message)
    {
        logger.LogInformation("Sending message: {message}", message);
        
        var messageString = JsonSerializer.Serialize(message);
        var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageString));
        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task BroadcastMessage(object message)
    {
        logger.LogInformation("Broadcasting message: {message}", message);
        
        foreach (var (key, socket) in Connections)
        {
            if (socket.State != WebSocketState.Open)
            {
                continue;
            }
            
            var messageString = JsonSerializer.Serialize(message);
            var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageString));
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}