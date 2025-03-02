using Chat.Api.Adapters;

namespace Chat.Api;

// As we cannot rely on the client to close the connection gracefully,
// We need to do a manual check to see if the user is still connected.
// We consider a lack of long poll for longer than KeepAliveTimerInMiliseconds to be a disconnect.
public class LongPollingConnectionStatusChecker(LongPollingUserRepository repo, MessagingService service, ILogger<LongPollingAdapter> logger) : BackgroundService
{
    private const int KeepAliveTimerInMiliseconds = 5000;

    protected override Task ExecuteAsync(CancellationToken ct) => Task.Run(async () =>
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var (name, user) in repo.Users)
            {
                if (DateTimeOffset.UtcNow - user.LastSeen > TimeSpan.FromMilliseconds(KeepAliveTimerInMiliseconds))
                {
                    logger.LogWarning("Disconnecting user {name} due to timeout", name);
                    await service.RemoveUser(name);
                    await user.Connection.CloseConnection("Timeout");
                }
            }
            
            await Task.Delay(KeepAliveTimerInMiliseconds, ct);
        }
    }, ct);
}