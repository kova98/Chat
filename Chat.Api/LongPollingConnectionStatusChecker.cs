namespace Chat.Api;

// As we cannot rely on the client to close the connection gracefully,
// We need to do a manual check to see if the user is still connected.
// This scales terribly, but the alternative is 
// We consider a lack of long poll for longer than KeepAliveTimerInMiliseconds to be a disconnect.
public class LongPollingConnectionStatusChecker(LongPollingUserRepository repo, MessagingService service) : BackgroundService
{
    private const int KeepAliveTimerInMiliseconds = 500;
    private const int CheckIntervalInMiliseconds = 1000;

    protected override Task ExecuteAsync(CancellationToken ct) => Task.Run(async () =>
    {
        while (!ct.IsCancellationRequested)
        {
            foreach (var (name, user) in repo.Users)
            {
                if (DateTimeOffset.UtcNow - user.LastSeen > TimeSpan.FromMilliseconds(KeepAliveTimerInMiliseconds))
                {
                    await service.RemoveUser(name);
                    await user.Connection.CloseConnection("Timeout");
                }
            }
            
            await Task.Delay(CheckIntervalInMiliseconds, ct);
        }
    }, ct);
}