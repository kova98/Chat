namespace Chat.Api;

// As we cannot really rely on the client to keep telling us if he's active, 
// we consider a lack of long poll for longer than KeepAliveTimerInMiliseconds to be a disconnect.
public class KeepAliveService(LongPollingConnectionRepository repo, MessagingService service) : BackgroundService
{
    private const int KeepAliveTimerInMiliseconds = 500;

    protected override Task ExecuteAsync(CancellationToken ct) => Task.Run(async () =>
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var connection in repo.Connections)
            {
                if (DateTimeOffset.UtcNow - connection.Value.LastSeen > TimeSpan.FromMilliseconds(KeepAliveTimerInMiliseconds))
                {
                    await service.RemoveUser(connection.Key);
                    await connection.Value.Connection.CloseConnection("Timeout");
                    repo.Connections.TryRemove(connection.Key, out _);
                }
            }
            
            await Task.Delay(1000, ct);
        }
    }, ct);
}