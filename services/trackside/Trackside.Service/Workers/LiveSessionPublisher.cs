using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Application.Persistence;
using Trackside.Domain.LiveSession;
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
    private readonly IOptionsMonitor<TracksidePersistenceOptions> _persistenceOptions;
    private readonly ILiveDataPublisher _liveDataPublisher;
    private readonly ILogger<LiveSessionPublisher> _logger;
    private string? _lastPersistedSnapshotFingerprint;

    /// <summary>
    /// Creates the background publisher.
    /// </summary>
    /// <param name="source">Configured live-session source.</param>
    /// <param name="state">Shared current-snapshot cache.</param>
    /// <param name="store">Durable Phase 2 store.</param>
    /// <param name="hubContext">SignalR hub context used for browser pushes.</param>
    /// <param name="options">Live application options used for publish cadence.</param>
    /// <param name="persistenceOptions">Persistence options used for default session inclusion.</param>
    /// <param name="liveDataPublisher">Publisher for projected live data consumed by optional modules.</param>
    /// <param name="logger">Logger for source failures and lifecycle events.</param>
    public LiveSessionPublisher(
        ILiveSessionSource source,
        LiveSessionState state,
        ITracksideStore store,
        IHubContext<LiveSessionHub, ILiveSessionClient> hubContext,
        IOptionsMonitor<TracksideLiveSessionOptions> options,
        IOptionsMonitor<TracksidePersistenceOptions> persistenceOptions,
        ILiveDataPublisher liveDataPublisher,
        ILogger<LiveSessionPublisher> logger)
    {
        _source = source;
        _state = state;
        _store = store;
        _hubContext = hubContext;
        _options = options;
        _persistenceOptions = persistenceOptions;
        _liveDataPublisher = liveDataPublisher;
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
                await _liveDataPublisher.PublishAsync(new ScoringContextFrame { Snapshot = snapshot }, stoppingToken);
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
            var countForHistory = _persistenceOptions.CurrentValue.CountSessionsByDefault;
            var fingerprint = BuildPersistenceFingerprint(snapshot, countForHistory);
            if (string.Equals(fingerprint, _lastPersistedSnapshotFingerprint, StringComparison.Ordinal))
            {
                return;
            }

            await _store.SaveLiveSessionSnapshotAsync(
                snapshot,
                countForHistory,
                cancellationToken);
            _lastPersistedSnapshotFingerprint = fingerprint;
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
        return TimeSpan.FromSeconds(publishSeconds);
    }

    private static string BuildPersistenceFingerprint(LiveSessionSnapshot snapshot, bool countForHistory)
    {
        var builder = new StringBuilder();
        builder.Append(countForHistory ? '1' : '0').Append('|');
        builder.Append(snapshot.Source).Append('|');
        builder.Append(snapshot.Session.TrackName).Append('|');
        builder.Append(snapshot.Session.Kind).Append('|');
        builder.Append(snapshot.Session.Phase).Append('|');
        AppendValue(builder, snapshot.Session.CurrentSessionSeconds);
        AppendValue(builder, snapshot.Session.ScheduledDurationSeconds);
        AppendValue(builder, snapshot.Session.LapDistanceMeters);
        builder.Append(snapshot.Session.OverallFlag).Append('|');

        foreach (var driver in snapshot.Drivers.OrderBy(driver => driver.DriverId, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(driver.DriverId).Append('|');
            builder.Append(driver.RigName).Append('|');
            builder.Append(driver.DisplayName).Append('|');
            builder.Append(driver.VehicleName).Append('|');
            builder.Append(driver.LeaderboardRank).Append('|');
            builder.Append(driver.Position).Append('|');
            builder.Append(driver.CompletedLaps).Append('|');
            AppendValue(builder, driver.BestLapSeconds);
            AppendValue(builder, driver.LastLapSeconds);
            AppendValue(builder, driver.CurrentLapSeconds);
            AppendValue(builder, driver.GapToLeaderSeconds);
            AppendValue(builder, driver.GapToNextSeconds);
            builder.Append(driver.LapsBehindLeader).Append('|');
            AppendValue(builder, driver.TrackPositionPercent);
            AppendValue(builder, driver.LapDistanceMeters);
            AppendValue(builder, driver.PosX);
            AppendValue(builder, driver.PosZ);
            foreach (var sector in driver.Sectors.OrderBy(sector => sector.Number))
            {
                builder.Append(sector.Number).Append('|');
                AppendValue(builder, sector.BestSeconds);
                AppendValue(builder, sector.LastSeconds);
                AppendValue(builder, sector.CurrentSeconds);
                builder.Append(sector.IsOverallBest ? '1' : '0').Append('|');
            }
        }

        return builder.ToString();
    }

    private static void AppendValue(StringBuilder builder, double? value)
    {
        if (value.HasValue && double.IsFinite(value.Value))
        {
            builder.Append(value.Value.ToString("R", CultureInfo.InvariantCulture));
        }

        builder.Append('|');
    }
}