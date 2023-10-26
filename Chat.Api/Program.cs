using Chat.Api;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel((_, options) =>
{
    // Port configured for WebTransport
    options.ListenAnyIP(5003, listenOptions =>
    {
        listenOptions.UseHttps(Certificate.GenerateManualCertificate());
        listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3;
    });
});
builder.Services.AddLogging();
builder.Services.AddSingleton<MessagingService>();
builder.Services.AddTransient<WebSocketAdapter>();
builder.Services.AddTransient<ServerSentEventsAdapter>();
builder.Services.AddTransient<LongPollingAdapter>();
builder.Services.AddSingleton<LongPollingUserRepository>();
builder.Services.AddHostedService<LongPollingConnectionStatusChecker>();
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

app.Map("/ws", async (string name, HttpContext context, WebSocketAdapter ws) =>
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

app.Map("/wt", async (string name, HttpContext context, CancellationToken ct, WebTransportAdapter adapter) =>
{
    var feature = context.Features.GetRequiredFeature<IHttpWebTransportFeature>();
    if (feature.IsWebTransportRequest)
    {
        var session = await feature.AcceptAsync(ct);
        await adapter.HandleWebTransportRequest(session, name);
    }
    else
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsync("Expected a WebSocket request");
    }
});

app.Map("sse", async (HttpContext context, CancellationToken ct, ServerSentEventsAdapter adapter, string name) =>
    await adapter.HandleServerSentEventsRequest(context, ct, name));

app.MapGet("/lp", async (HttpContext context, CancellationToken ct, LongPollingAdapter adapter, string name, string? id) =>
    await adapter.HandleLongPollingRequest(context, ct, name, id));

app.MapPost("lp/message", async (MessagingService service, ChatMessage message) =>
{
    await service.BroadcastMessage(message);
    return Results.Ok();
});

app.Run(async (context) =>
{
    var feature = context.Features.GetRequiredFeature<IHttpWebTransportFeature>();
    if (!feature.IsWebTransportRequest)
    {
        return;
    }
    var session = await feature.AcceptAsync(CancellationToken.None);

    // open a new stream from the server to the client
    var stream = await session.OpenUnidirectionalStreamAsync(CancellationToken.None);

    if (stream is not null)
    {
        // write data to the stream
        var outputPipe = stream.Transport.Output;
        await outputPipe.WriteAsync(new Memory<byte>(new byte[] { 65, 66, 67, 68, 69 }), CancellationToken.None);
        await outputPipe.FlushAsync(CancellationToken.None);
    }
});

app.Run();
