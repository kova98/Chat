using System.Collections.Concurrent;

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
        var everyoneElse = Connections.Where(x => x.Key != name).Select(x => x.Value);
        await BroadcastMessage(userConnected, everyoneElse);

        await SendMessage(connection, new History(History.TakeLast(50)));
        await SendMessage(connection, new UserList(Connections.Keys));

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

    public Task RemoveUser(string name)
    {
        Connections.TryRemove(name, out _);
        var msg = new UserDisconnected(name);
        return BroadcastMessage(msg);
    }

    public Task SendMessage(IConnectionAdapter connection, Message message)
    {
        logger.LogInformation("Sending message: {message}", message);

        return connection.SendMessage(message);
    }
    
    public async Task BroadcastMessage(Message message, IEnumerable<IConnectionAdapter>? receivers = null)
    {
        logger.LogInformation("Broadcasting message: {message}", message);
     
        History.Enqueue(message);
        
        foreach (var connection in receivers ?? Connections.Values)
        {
            await connection.SendMessage(message);
        }
    }
}