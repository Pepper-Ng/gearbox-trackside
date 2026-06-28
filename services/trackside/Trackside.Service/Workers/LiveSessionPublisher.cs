using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Application.Persistence;
using Trackside.Domain.LiveSession;
using Trackside.Service.Configuration;
using Trackside.Service.Hubs;

namespace Trackside.Service.Workers;

/// <summary>
/// Periodically refreshes the current session and broadcasts it to SignalR clients.
/// </summary>
public sealed class LiveSessionPublisher : BackgroundService
{
    private readonly ILiveSessionSource _source;
    private readonly LiveSessionState _state;
    private readonly ITracksideStore _store;
    private readonly IHubContext<LiveSessionHub, ILiveSessionClient> _hubContext;
    private readonly IOptionsMonitor<TracksideLiveSessionOptions> _options;
    private readonly IOptionsMonitor<TracksideOptions> _tracksideOptions;
    private readonly IOptionsMonitor<TracksidePersistenceOptions> _persistenceOptions;
    private readonly ILogger<LiveSessionPublisher> _logger;

    /// <summary>
    /// Creates the background publisher.
    /// </summary>
    /// <param name="source">Configured live-session source.</param>
    /// <param name="state">Shared current-snapshot cache.</param>
    /// <param name="store">Durable Phase 2 store.</param>
    /// <param name="hubContext">SignalR hub context used for browser pushes.</param>
    /// <param name="options">Live application options used for publish cadence.</param>
    /// <param name="persistenceOptions">Persistence options used for default session inclusion.</param>
    /// <param name="logger">Logger for source failures and lifecycle events.</param>
    public LiveSessionPublisher(
        ILiveSessionSource source,
        LiveSessionState state,
        ITracksideStore store,
        IHubContext<LiveSessionHub, ILiveSessionClient> hubContext,
        IOptionsMonitor<TracksideLiveSessionOptions> options,
        IOptionsMonitor<TracksideOptions> tracksideOptions,
        IOptionsMonitor<TracksidePersistenceOptions> persistenceOptions,
        ILogger<LiveSessionPublisher> logger)
    {
        _source = source;
        _state = state;
        _store = store;
        _hubContext = hubContext;
        _options = options;
        _tracksideOptions = tracksideOptions;
        _persistenceOptions = persistenceOptions;
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
                await PersistSnapshotAsync(snapshot, stoppingToken);
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

    private async Task PersistSnapshotAsync(LiveSessionSnapshot snapshot, CancellationToken cancellationToken)
    {
        try
        {
            await _store.SaveLiveSessionSnapshotAsync(
                snapshot,
                _persistenceOptions.CurrentValue.CountSessionsByDefault,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist live-session snapshot.");
        }
    }

    private TimeSpan GetPublishInterval()
    {
        var publishSeconds = Math.Max(
            TracksideLiveSessionOptions.MinimumPublishIntervalSeconds,
            _options.CurrentValue.PublishIntervalSeconds);

        var tracksideOptions = _tracksideOptions.CurrentValue;
        if (tracksideOptions.Source.Mode == LiveSessionSourceMode.SharedMemory)
        {
            var scoringSeconds = 1.0 / Math.Clamp(tracksideOptions.Source.SharedMemory.ScoringPollHz, 0.25, 200.0);
            return TimeSpan.FromSeconds(Math.Min(publishSeconds, scoringSeconds));
        }

        return TimeSpan.FromSeconds(publishSeconds);
    }
}