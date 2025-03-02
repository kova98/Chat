using System.Collections.Concurrent;
using Chat.Api.Adapters;

namespace Chat.Api;

public class LongPollingUser(LongPollingAdapter connection, DateTimeOffset lastSeen, Guid id)
{
    public LongPollingAdapter Connection { get; set; } = connection;
    public DateTimeOffset LastSeen { get; set; } = lastSeen;
    public Guid Id { get; } = id;
}

// Singleton repository allowing Transient LongPollingAdapter instances to share state.
public class LongPollingUserRepository()
{
    public readonly ConcurrentDictionary<string, LongPollingUser> Users = new();
    public readonly ConcurrentDictionary<string, List<Message>> Buffer = new();
}