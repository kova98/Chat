using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Chat.Api.Adapters;

namespace Chat.Api;

public class MessagingService : IAsyncDisposable
{
    private const string HistoryFile = "/data/messages.log";
    private const int HistorySize = 500;

    private readonly ConcurrentDictionary<string, IConnectionAdapter> Connections = new();
    private readonly ConcurrentQueue<Message> History = new();
    private readonly ILogger<MessagingService> _logger;
    private readonly FileStream _appendStream;


    public MessagingService(ILogger<MessagingService> logger)
    {
        _logger = logger;
        _appendStream = new FileStream(HistoryFile, FileMode.Append, FileAccess.Write, FileShare.Read);
        LoadHistory();
    }

    private void LoadHistory()
    {
        using var reader = new StreamReader(new FileStream(HistoryFile, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite));
        string? line;

        var lastLines = new List<string>();
        while ((line = reader.ReadLine()) != null)
        {
            // Get the last HistorySize messages
            if (lastLines.Count > HistorySize) break;
            lastLines.Add(line);
        }

        foreach (var messageLine in lastLines)
        {
            if (JsonSerializer.Deserialize<Message>(messageLine) is Message message)
            {
                _logger.LogInformation("Loading message: {message}, type {type}", message, message.GetType().Name);
                History.Enqueue(message);
            }
        }
    }

    public async Task<string?> TryAddUser(IConnectionAdapter connection, string name)
    {
        if (!TryAddUser(name, connection))
        {
            return $"Name '{name}' already taken";
        }

        var userConnected = new UserConnected(name, GetTransport(connection));
        var everyoneElse = Connections.Where(x => x.Key != name).Select(x => x.Value);
        await BroadcastMessage(userConnected, everyoneElse);

        await SendMessage(connection, new History(History.TakeLast(100)));
        await SendMessage(connection, new UserList(Connections.Keys));

        return null;
    }

    private string GetTransport(IConnectionAdapter conn) => conn switch
    {
        WebSocketAdapter _ => "WebSocket",
        ServerSentEventsAdapter _ => "Server-Sent Events",
        LongPollingAdapter _ => "Long Polling",
        _ => "Unknown transport"
    };

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
        _logger.LogInformation("Sending message: {message}", message);

        return connection.SendMessage(message);
    }

    public async Task BroadcastMessage(Message message, IEnumerable<IConnectionAdapter>? receivers = null)
    {
        _logger.LogInformation("Broadcasting message: {message}", message);

        History.Enqueue(message);

        foreach (var connection in receivers ?? Connections.Values)
        {
            await connection.SendMessage(message);
        }

        // Append message, don't wait for it to finish
        _ = AppendMessageToHistory(message);
    }

    private async Task AppendMessageToHistory(Message message)
    {
        var json = JsonSerializer.Serialize(message) + Environment.NewLine;
        var bytes = Encoding.UTF8.GetBytes(json);
        await _appendStream.WriteAsync(bytes);
        await _appendStream.FlushAsync(); // Ensure it's written to disk
    }

    public async ValueTask DisposeAsync()
    {
        if (_appendStream != null)
        {
            await _appendStream.FlushAsync();
            await _appendStream.DisposeAsync();
        }
    }
}