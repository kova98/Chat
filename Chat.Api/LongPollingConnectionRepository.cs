using System.Collections.Concurrent;

namespace Chat.Api;

public class LongPollingConnection(LongPollingAdapter connection, DateTimeOffset lastSeen, Guid id)
{
    public LongPollingAdapter Connection { get; } = connection;
    public DateTimeOffset LastSeen { get; set; } = lastSeen;

    public Guid Id { get; } = id;
}

// Singleton repository allowing Transient LongPollingAdapter instances to share state.
public class LongPollingConnectionRepository()
{
    public readonly ConcurrentDictionary<string, LongPollingConnection> Connections = new();
    
    public readonly ConcurrentDictionary<string, List<Message>> Buffers = new();
}