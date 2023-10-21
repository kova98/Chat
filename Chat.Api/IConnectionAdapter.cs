namespace Chat.Api;


public interface IConnectionAdapter
{
    Task CloseConnection(string reason);
    Task SendMessage(string messageString);
}