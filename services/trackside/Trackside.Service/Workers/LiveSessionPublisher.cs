using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Service.Hubs;

namespace Trackside.Service.Workers;

/// <summary>
/// Periodically refreshes the current session and broadcasts it to SignalR clients.
/// </summary>
public sealed class LiveSessionPublisher : BackgroundService
{
    private readonly ILiveSessionSource _source;
    private readonly LiveSessionState _state;
    private readonly IHubContext<LiveSessionHub, ILiveSessionClient> _hubContext;
    private readonly IOptionsMonitor<TracksideLiveSessionOptions> _options;
    private readonly ILogger<LiveSessionPublisher> _logger;

    /// <summary>
    /// Creates the background publisher.
    /// </summary>
    /// <param name="source">Configured live-session source.</param>
    /// <param name="state">Shared current-snapshot cache.</param>
    /// <param name="hubContext">SignalR hub context used for browser pushes.</param>
    /// <param name="options">Live application options used for publish cadence.</param>
    /// <param name="logger">Logger for source failures and lifecycle events.</param>
    public LiveSessionPublisher(
        ILiveSessionSource source,
        LiveSessionState state,
        IHubContext<LiveSessionHub, ILiveSessionClient> hubContext,
        IOptionsMonitor<TracksideLiveSessionOptions> options,
        ILogger<LiveSessionPublisher> logger)
    {
        _source = source;
        _state = state;
        _hubContext = hubContext;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Live-session publisher started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await _source.GetCurrentAsync(stoppingToken);
                _state.Update(snapshot);
                await _hubContext.Clients.All.SessionUpdated(snapshot);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh or publish the live-session snapshot.");
            }

            await Task.Delay(GetPublishInterval(), stoppingToken);
        }
    }

    private TimeSpan GetPublishInterval()
    {
        var seconds = Math.Max(
            TracksideLiveSessionOptions.MinimumPublishIntervalSeconds,
            _options.CurrentValue.PublishIntervalSeconds);

        return TimeSpan.FromSeconds(seconds);
    }
}