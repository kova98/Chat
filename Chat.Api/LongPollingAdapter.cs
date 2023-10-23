﻿using System.Net;
using System.Text.Json;

namespace Chat.Api;

public class LongPollingAdapter(MessagingService service, LongPollingUserRepository repo) : IConnectionAdapter
{
    private const int TimeoutInSeconds = 30;
    
    private HttpContext _context;
    private string _name;
    private CancellationTokenSource _timeoutCts;
    
    public async Task HandleLongPollingRequest(HttpContext ctx, CancellationToken userCt, string name, string? idString)
    {
        _ = Guid.TryParse(idString, out var id);
        
        _name = name;
        _context = ctx;
        
        _timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken());
        _timeoutCts.CancelAfter(TimeSpan.FromSeconds(TimeoutInSeconds));
        
        var userExists = repo.Users.TryGetValue(name, out var existingUser);
        if (userExists && existingUser!.Id != id)
        {
            // New user, same name
            ctx.Response.StatusCode = 400;
            await ctx.Response.WriteAsync($"Name '{name}' already taken", userCt);
            return;
        }
        
        if (!userExists)
        {
            // Completely new user
            id = Guid.NewGuid();
            existingUser = new LongPollingUser(this, DateTimeOffset.UtcNow, id);
            repo.Users.TryAdd(name, existingUser);
            
            var error = await service.TryAddUser(this, name);
            if (error != null)
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync(error, userCt);
                return;
            }
            
        }
        // Existing user, retry after timeout

        ctx.Response.Headers.Add("X-Connection-Id", id.ToString());
        
        while (!_timeoutCts.IsCancellationRequested)
        {
            if (userCt.IsCancellationRequested)
            {
                // User closed the connection (left the page or closed the browser)
                await service.RemoveUser(_name);
                repo.Users.TryRemove(_name, out _);
                return;
            }        
            
            if (repo.Buffer.TryGetValue(_name, out var buffer) && buffer.Count > 0 )
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                await JsonSerializer.SerializeAsync(ctx.Response.Body, buffer, cancellationToken: userCt);
                buffer.Clear();
                return;
            }
            
            existingUser.LastSeen = DateTimeOffset.UtcNow;

            // smaller delay makes server more responsive, but decreases performance
            await Task.Delay(50);
        }
        
        ctx.Response.StatusCode = (int)HttpStatusCode.NoContent;
    }
    
    public async Task CloseConnection(string reason)
    {
        await _timeoutCts.CancelAsync();
        repo.Users.TryRemove(_name, out _);
    }

    public Task SendMessage(Message message)
    {
        repo.Buffer
            .GetOrAdd(_name, _ => new List<Message> { message })
            .Add(message);
        
        return Task.CompletedTask;
    }
}