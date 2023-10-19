using Chat.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddSingleton<WebSocketService>();

var app = builder.Build();
app.UseWebSockets();
app.MapGet("/" , () => "WebSocket server");
app.Map("/ws", async (HttpContext context, string name, WebSocketService ws) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        await ws.HandleWebSocket(context, name);
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Expected a WebSocket request");
    }
});

app.Run();