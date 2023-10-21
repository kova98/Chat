using System.Collections.Concurrent;

namespace Chat.Api;

public class LongPollingConnection(IConnectionAdapter connection, DateTimeOffset lastSeen, Guid id)
{
    public IConnectionAdapter Connection { get; set; } = connection;
    public DateTimeOffset LastSeen { get; set; } = lastSeen;

    public Guid Id { get; set; } = id;
}

// Singleton repository allowing Transient LongPollingAdapter instances to share state.
public class LongPollingConnectionRepository()
{
    public readonly ConcurrentDictionary<string, LongPollingConnection> Connections = new();
    
    public readonly ConcurrentDictionary<string, List<Message>> Buffers = new();
}