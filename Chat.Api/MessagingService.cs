using System.Collections.Concurrent;
using System.Text.Json;

namespace Chat.Api;

public class MessagingService(ILogger<MessagingService> logger)
{
    private readonly ConcurrentDictionary<string, IConnectionAdapter> Connections = new();
    private readonly ConcurrentQueue<Message> History = new();
    
    public async Task<string?> TryAddUser(IConnectionAdapter connection, string name)
    {
        if (!TryAddUser(name, connection))
        {
            return $"Name '{name}' already taken";
        }
        
        var userConnected = new UserConnected(name);
        var everyoneElse = Connections.Where(x => x.Key != name).Select(x => x.Value).ToArray();
        await BroadcastMessage(userConnected, everyoneElse);

        await SendMessage(connection, new History(History.ToArray()));
        await SendMessage(connection, new UserList(Connections.Keys.ToArray()));

        return null;
    }
    
    private bool TryAddUser(string name, IConnectionAdapter connection)
    {
        if (Connections.ContainsKey(name))
        {
            return false;
        }

        Connections.TryAdd(name, connection);
        
        return true;
    }

    public async Task RemoveUser(string name)
    {
        Connections.TryRemove(name, out _);
        var msg = new UserDisconnected(name);
        History.Enqueue(msg);
        await BroadcastMessage(msg);
    }

    private async Task SendMessage(IConnectionAdapter connection, Message message)
    {
        logger.LogInformation("Sending message: {message}", message);

        var messageString = JsonSerializer.Serialize(message);
        await connection.SendMessage(messageString);
    }
    
    public async Task BroadcastMessage(Message message, IEnumerable<IConnectionAdapter>? receivers = null)
    {
        logger.LogInformation("Broadcasting message: {message}", message);
     
        History.Enqueue(message);
        
        var messageString = JsonSerializer.Serialize(message);
        
        foreach (var connection in receivers ?? Connections.Values)
        {
            await connection.SendMessage(messageString);
        }
    }
}