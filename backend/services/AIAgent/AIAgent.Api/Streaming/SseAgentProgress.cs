using System.Text.Json;
using AIAgent.Application.Interfaces;

namespace AIAgent.Api.Streaming;





public sealed class SseAgentProgress : IAgentProgress, IAsyncDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpResponse _response;
    private readonly CancellationToken _aborted;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private Task? _keepAlive;

    public SseAgentProgress(HttpResponse response, CancellationToken aborted)
    {
        _response = response;
        _aborted = aborted;
    }

    
    public TimeSpan KeepAliveEvery { get; set; } = TimeSpan.FromSeconds(10);

    public async Task StartAsync()
    {
        _response.StatusCode = StatusCodes.Status200OK;
        _response.ContentType = "text/event-stream; charset=utf-8";
        _response.Headers.CacheControl = "no-cache, no-transform";
        _response.Headers["X-Accel-Buffering"] = "no";   
        _response.HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpResponseBodyFeature>()?.DisableBuffering();

        await WriteAsync(": open\n\n");
        _keepAlive = Task.Run(KeepAliveLoopAsync);
    }

    public Task StatusAsync(string text) => SendAsync("status", new { text });
    public Task TextAsync(string delta) => SendAsync("delta", new { text = delta });
    public Task DiscardTextAsync() => SendAsync("reset", new { });

    public Task SendAsync(string eventName, object data) =>
        WriteAsync($"event: {eventName}\ndata: {JsonSerializer.Serialize(data, Json)}\n\n");

    private async Task WriteAsync(string frame)
    {
        await _gate.WaitAsync(_aborted);
        try
        {
            await _response.WriteAsync(frame, _aborted);
            await _response.Body.FlushAsync(_aborted);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task KeepAliveLoopAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(KeepAliveEvery);
            while (await timer.WaitForNextTickAsync(_stop.Token)) await WriteAsync(": keep-alive\n\n");
        }
        catch (Exception)
        {
            
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        if (_keepAlive is not null) await _keepAlive;
        _stop.Dispose();
        _gate.Dispose();
    }
}
