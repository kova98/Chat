namespace Chat.Api;

// TODO: solve memory allocation issues
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
                    repo.Connections.TryRemove(connection.Key, out _);
                }
            }
            
            await Task.Delay(1000, ct);
        }
    }, ct);
}