using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Chat.Api;

public class WebSocketService
{
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
            
        await Receive(socket, async (result, buffer) =>
        {
            switch (result.MessageType)
            {
                case WebSocketMessageType.Text:
                    var messageString = Encoding.UTF8.GetString(buffer);
                    var message = JsonSerializer.Deserialize<Message>(messageString);
                    await BroadcastMessage(message);
                    return;
                case WebSocketMessageType.Close:
                    Connections.TryRemove(name, out _);
                    await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                    return;
                case WebSocketMessageType.Binary:
                default:
                    return;
            }
        });
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

    private static async Task BroadcastMessage(Message? message)
    {
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