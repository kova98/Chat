using Chat.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHostedService<KeepAliveService>();
builder.Services.AddLogging();
builder.Services.AddTransient<WebSocketAdapter>();
builder.Services.AddSingleton<MessagingService>();
builder.Services.AddTransient<LongPollingAdapter>();
builder.Services.AddSingleton<LongPollingConnectionRepository>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAnyOrigin", p => p
        .AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()
        .WithExposedHeaders("X-Connection-Id"));
});

var app = builder.Build();
app.UseCors("AllowAnyOrigin");
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

app.MapGet("/lp", async (HttpContext context, CancellationToken ct, LongPollingAdapter service, string name, string? id) =>
    await service.HandleLongPollingRequest(context, ct, name, id));

app.MapPost("lp/message", async (MessagingService service, ChatMessage message) =>
{
    await service.BroadcastMessage(message);

    return Results.Ok();
});

app.Run();