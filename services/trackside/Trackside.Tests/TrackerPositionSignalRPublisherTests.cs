using Microsoft.Extensions.Options;
using Trackside.Application.LiveSession;
using Trackside.Domain.LiveSession;
using Trackside.Service.Configuration;
using Trackside.Service.Hubs;

namespace Trackside.Tests;

/// <summary>
/// Verifies compact high-rate Tracker position projection before SignalR rate limiting.
/// </summary>
public sealed class TrackerPositionSignalRPublisherTests
{
    /// <summary>
    /// Scoring frames provide a smooth fallback when the telemetry loop is disabled.
    /// </summary>
    [Fact]
    public async Task ScoringFrameProjectsPositionsWithoutTelemetry()
    {
        var now = DateTimeOffset.Parse("2026-07-19T12:00:00+00:00");
        var time = new ManualTimeProvider(now);
        var publisher = CreatePublisher(time);

        await publisher.ConsumeAsync(new ScoringContextFrame
        {
            Snapshot = Snapshot(now, currentSessionSeconds: 45.0, posX: 12.5, posZ: -8.0),
        }, CancellationToken.None);

        var update = Assert.IsType<TrackerPositionUpdate>(publisher.Latest);
        Assert.Equal("Loch Drummond - Short", update.TrackName);
        Assert.Equal(SessionPhase.GreenFlag, update.Phase);
        Assert.Equal(45.0, update.CurrentSessionSeconds);
        var vehicle = Assert.Single(update.Vehicles);
        Assert.Equal("7", vehicle.DriverId);
        Assert.Equal(12.5, vehicle.PosX);
        Assert.Equal(-8.0, vehicle.PosZ);
    }

    /// <summary>
    /// Telemetry frames replace scoring positions while retaining current scoring session metadata.
    /// </summary>
    [Fact]
    public async Task TelemetryFrameUsesLatestScoringMetadata()
    {
        var now = DateTimeOffset.Parse("2026-07-19T12:00:00+00:00");
        var time = new ManualTimeProvider(now);
        var publisher = CreatePublisher(time);
        await publisher.ConsumeAsync(new ScoringContextFrame
        {
            Snapshot = Snapshot(now, currentSessionSeconds: 45.0, posX: 12.5, posZ: -8.0),
        }, CancellationToken.None);

        time.Advance(TimeSpan.FromMilliseconds(50));
        await publisher.ConsumeAsync(new TelemetryPositionFrame
        {
            TrackName = "Loch Drummond - Short",
            Source = "telemetry",
            Vehicles = [new TelemetryPositionVehicle { DriverId = "7", PosX = 13.25, PosY = 0.5, PosZ = -7.25 }],
        }, CancellationToken.None);

        var update = Assert.IsType<TrackerPositionUpdate>(publisher.Latest);
        Assert.Equal("telemetry", update.Source);
        Assert.Equal(45.05, update.CurrentSessionSeconds!.Value, precision: 3);
        var vehicle = Assert.Single(update.Vehicles);
        Assert.Equal(13.25, vehicle.PosX);
        Assert.Equal(0.5, vehicle.PosY);
        Assert.Equal(-7.25, vehicle.PosZ);
    }

    /// <summary>
    /// Telemetry from a different or stale track cannot move markers for the current session.
    /// </summary>
    [Fact]
    public async Task IgnoresTelemetryForDifferentTrack()
    {
        var now = DateTimeOffset.Parse("2026-07-19T12:00:00+00:00");
        var publisher = CreatePublisher(new ManualTimeProvider(now));
        await publisher.ConsumeAsync(new ScoringContextFrame
        {
            Snapshot = Snapshot(now, currentSessionSeconds: 45.0, posX: 12.5, posZ: -8.0),
        }, CancellationToken.None);
        var scoringSequence = publisher.Latest!.Sequence;

        await publisher.ConsumeAsync(new TelemetryPositionFrame
        {
            TrackName = "Another Track",
            Source = "telemetry",
            Vehicles = [new TelemetryPositionVehicle { DriverId = "7", PosX = 999.0, PosZ = 999.0 }],
        }, CancellationToken.None);

        Assert.Equal(scoringSequence, publisher.Latest!.Sequence);
        Assert.Equal(12.5, Assert.Single(publisher.Latest.Vehicles).PosX);
    }

    private static TrackerPositionSignalRPublisher CreatePublisher(TimeProvider timeProvider) => new(
        hubContext: null!,
        new StaticOptionsMonitor<TracksideDriverTrackerOptions>(new TracksideDriverTrackerOptions { ClientRefreshHz = 100.0 }),
        timeProvider);

    private static LiveSessionSnapshot Snapshot(
        DateTimeOffset timestampUtc,
        double currentSessionSeconds,
        double posX,
        double posZ) => new()
    {
        Source = "shared-memory",
        Status = "connected",
        TimestampUtc = timestampUtc,
        Session = new LiveSessionInfo
        {
            TrackName = "Loch Drummond - Short",
            Kind = SessionKind.Practice,
            Phase = SessionPhase.GreenFlag,
            CurrentSessionSeconds = currentSessionSeconds,
            ScheduledDurationSeconds = 1200.0,
            LapDistanceMeters = 1000.0,
            VehicleCount = 1,
        },
        Drivers =
        [
            new DriverSnapshot
            {
                DriverId = "7",
                RigName = "Setup1",
                DisplayName = "Maya",
                VehicleName = "Formula Pro",
                PosX = posX,
                PosY = 0.25,
                PosZ = posZ,
            },
        ],
    };

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan value) => _utcNow += value;
    }
}
