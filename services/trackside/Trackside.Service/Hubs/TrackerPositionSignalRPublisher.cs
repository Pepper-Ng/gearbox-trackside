using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Trackside.Application.LiveSession;
using Trackside.Domain.LiveSession;
using Trackside.Service.Configuration;

namespace Trackside.Service.Hubs;

/// <summary>
/// Projects scoring or telemetry positions into compact, rate-limited SignalR updates for Tracker clients.
/// </summary>
public sealed class TrackerPositionSignalRPublisher : BackgroundService,
    ILiveDataConsumer<ScoringContextFrame>,
    ILiveDataConsumer<TelemetryPositionFrame>
{
    /// <summary>
    /// SignalR group containing browser clients currently displaying Tracker content.
    /// </summary>
    public const string TrackerClientGroup = "tracker-positions";

    private readonly IHubContext<LiveSessionHub, ILiveSessionClient> _hubContext;
    private readonly IOptionsMonitor<TracksideDriverTrackerOptions> _options;
    private readonly TimeProvider _timeProvider;
    private TrackerSessionMetadata? _latestSession;
    private TrackerPositionUpdate? _latestUpdate;
    private long _nextSequence;

    /// <summary>
    /// Creates the compact Tracker position publisher.
    /// </summary>
    public TrackerPositionSignalRPublisher(
        IHubContext<LiveSessionHub, ILiveSessionClient> hubContext,
        IOptionsMonitor<TracksideDriverTrackerOptions> options,
        TimeProvider timeProvider)
    {
        _hubContext = hubContext;
        _options = options;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Latest compact update awaiting rate-limited SignalR publication.
    /// </summary>
    public TrackerPositionUpdate? Latest => Volatile.Read(ref _latestUpdate);

    /// <inheritdoc />
    public ValueTask ConsumeAsync(ScoringContextFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = frame.Snapshot;
        var metadata = TrackerSessionMetadata.From(snapshot);
        Volatile.Write(ref _latestSession, metadata);

        var vehicles = snapshot.Drivers
            .Where(driver => IsFinite(driver.PosX) && IsFinite(driver.PosZ))
            .Select(driver => new TrackerPositionVehicle
            {
                DriverId = driver.DriverId,
                PosX = driver.PosX!.Value,
                PosY = driver.PosY,
                PosZ = driver.PosZ!.Value,
            })
            .ToList();
        SetLatest(metadata, vehicles, snapshot.Source);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ConsumeAsync(TelemetryPositionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var metadata = Volatile.Read(ref _latestSession);
        if (metadata is null
            || metadata.Phase is not SessionPhase.GreenFlag
            || !string.Equals(metadata.TrackName, frame.TrackName, StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.CompletedTask;
        }

        var vehicles = frame.Vehicles
            .Where(vehicle => double.IsFinite(vehicle.PosX) && double.IsFinite(vehicle.PosZ))
            .Select(vehicle => new TrackerPositionVehicle
            {
                DriverId = vehicle.DriverId,
                PosX = vehicle.PosX,
                PosY = vehicle.PosY,
                PosZ = vehicle.PosZ,
            })
            .ToList();
        SetLatest(metadata, vehicles, frame.Source);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        long lastPublishedSequence = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(GetPublishInterval(), stoppingToken);
            var update = Volatile.Read(ref _latestUpdate);
            if (update is null || update.Sequence == lastPublishedSequence)
            {
                continue;
            }

            lastPublishedSequence = update.Sequence;
            await _hubContext.Clients.Group(TrackerClientGroup).TrackerPositionsUpdated(update);
        }
    }

    private void SetLatest(
        TrackerSessionMetadata metadata,
        IReadOnlyList<TrackerPositionVehicle> vehicles,
        string source)
    {
        var now = _timeProvider.GetUtcNow();
        var currentSessionSeconds = metadata.CurrentSessionSeconds;
        if (currentSessionSeconds.HasValue && metadata.Phase is SessionPhase.GreenFlag)
        {
            var sourceAgeSeconds = (now - metadata.ObservedUtc).TotalSeconds;
            if (sourceAgeSeconds is >= 0.0 and <= 5.0)
            {
                currentSessionSeconds += sourceAgeSeconds;
            }
        }

        Volatile.Write(ref _latestUpdate, new TrackerPositionUpdate
        {
            Sequence = Interlocked.Increment(ref _nextSequence),
            TimestampUtc = now,
            Source = source,
            TrackName = metadata.TrackName,
            Phase = metadata.Phase,
            CurrentSessionSeconds = currentSessionSeconds,
            ScheduledDurationSeconds = metadata.ScheduledDurationSeconds,
            Vehicles = vehicles,
        });
    }

    private TimeSpan GetPublishInterval()
    {
        var hertz = Math.Clamp(
            _options.CurrentValue.ClientRefreshHz,
            TracksideDriverTrackerOptions.MinimumClientRefreshHz,
            TracksideDriverTrackerOptions.MaximumClientRefreshHz);
        return TimeSpan.FromSeconds(1.0 / hertz);
    }

    private static bool IsFinite(double? value) => value.HasValue && double.IsFinite(value.Value);

    private sealed record TrackerSessionMetadata
    {
        public string TrackName { get; init; } = string.Empty;
        public SessionPhase Phase { get; init; }
        public DateTimeOffset ObservedUtc { get; init; }
        public double? CurrentSessionSeconds { get; init; }
        public double? ScheduledDurationSeconds { get; init; }

        public static TrackerSessionMetadata From(LiveSessionSnapshot snapshot) => new()
        {
            TrackName = snapshot.Session.TrackName,
            Phase = snapshot.Session.Phase,
            ObservedUtc = snapshot.TimestampUtc,
            CurrentSessionSeconds = snapshot.Session.CurrentSessionSeconds,
            ScheduledDurationSeconds = snapshot.Session.ScheduledDurationSeconds,
        };
    }
}

/// <summary>
/// Compact high-rate browser update for current Tracker positions and session time.
/// </summary>
public sealed record TrackerPositionUpdate
{
    /// <summary>Monotonic process-local update sequence.</summary>
    public long Sequence { get; init; }

    /// <summary>UTC timestamp when this compact update was projected.</summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>Projected source, such as scoring or telemetry.</summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>Track associated with the position rows.</summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>Current coarse session phase.</summary>
    public SessionPhase Phase { get; init; }

    /// <summary>Current elapsed session time in seconds.</summary>
    public double? CurrentSessionSeconds { get; init; }

    /// <summary>Scheduled session duration in seconds, when known.</summary>
    public double? ScheduledDurationSeconds { get; init; }

    /// <summary>Compact driver world positions keyed by stable source id.</summary>
    public IReadOnlyList<TrackerPositionVehicle> Vehicles { get; init; } = [];
}

/// <summary>
/// One driver's compact world position in a Tracker update.
/// </summary>
public sealed record TrackerPositionVehicle
{
    /// <summary>Stable scoring/telemetry vehicle id.</summary>
    public string DriverId { get; init; } = string.Empty;

    /// <summary>World X coordinate.</summary>
    public double PosX { get; init; }

    /// <summary>Optional world Y coordinate.</summary>
    public double? PosY { get; init; }

    /// <summary>World Z coordinate.</summary>
    public double PosZ { get; init; }
}
