namespace Chat.Api.Adapters;

public interface IConnectionAdapter
{
    Task SendMessage(Message message);
}