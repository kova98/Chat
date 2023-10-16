using Chat.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddSingleton<WebSocketService>();

var app = builder.Build();
app.UseWebSockets();

app.MapGet("/ws", async (HttpContext context, WebSocketService ws) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        await ws.HandleWebSocket(context);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

app.Run();