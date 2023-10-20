using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Chat.Api;

public class WebSocketService(ILogger<WebSocketService> logger)
{
    private static readonly ConcurrentQueue<Message> History = new();
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
        
        var everyoneElse = Connections.Where(x => x.Key != name).Select(x => x.Value).ToArray();
        var userConnected = new UserConnected(name);
        await BroadcastMessage(userConnected, everyoneElse);
        History.Enqueue(userConnected);
        
        await SendMessage(socket, new History(History.ToArray()));
        await SendMessage(socket, new UserList(Connections.Keys.ToArray()));

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
        Connections.TryRemove(name, out _);
        var msg = new UserDisconnected(name);
        History.Enqueue(msg);
        await BroadcastMessage(msg);
    }

    private async Task Receive(WebSocket socket, Action<WebSocketReceiveResult, ArraySegment<byte>> handleMessage)
    {
        var buffer = new byte[1024 * 2];
        while (socket.State == WebSocketState.Open)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), default);
            if (result.EndOfMessage == false)
            {
                var name = Connections.FirstOrDefault(x => x.Value == socket).Key;
                await RemoveUser(name);
                await socket.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Max message size is 2KiB.", default);
                return;
            }
            handleMessage(result, new ArraySegment<byte>(buffer, 0, result.Count));
        }
    }

    private async Task SendMessage(WebSocket socket, Message message)
    {
        logger.LogInformation("Sending message: {message}", message);
        
        var messageString = JsonSerializer.Serialize(message);
        var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageString));
        await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    private async Task BroadcastMessage(Message message, IEnumerable<WebSocket>? receivers = null)
    {
        logger.LogInformation("Broadcasting message: {message}", message);
        
        var messageString = JsonSerializer.Serialize(message);
        var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageString));
        
        foreach (var socket in receivers ?? Connections.Values)
        {
            if (socket.State != WebSocketState.Open)
            {
                continue;
            }
            
            await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }
}