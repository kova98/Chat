using Chat.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddSingleton<WebSocketService>();

builder.Services.AddTransient<WebSocketAdapter>();
builder.Services.AddSingleton<MessagingService>();
var app = builder.Build();
app.UseWebSockets();
app.MapGet("/" , () => "WebSocket server");
app.Map("/ws", async (HttpContext context, string name, WebSocketAdapter ws) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        await ws.HandleUser(context, name);
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Expected a WebSocket request");
    }
});

app.Run();