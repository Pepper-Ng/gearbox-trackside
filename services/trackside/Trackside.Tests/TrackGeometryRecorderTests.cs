using Microsoft.Extensions.Options;
using Trackside.Application.LiveSession;
using Trackside.Domain.LiveSession;
using Trackside.Service.Configuration;
using Trackside.Service.Hosting;
using Trackside.Service.Tracking;

namespace Trackside.Tests;

/// <summary>
/// Covers generated track geometry used by the driver tracker page.
/// </summary>
public sealed class TrackGeometryRecorderTests
{
    /// <summary>
    /// Sparse samples are persisted as evidence but are not exposed as drawable geometry.
    /// </summary>
    [Fact]
    public void SparseSamplesDoNotProduceAvailableGeometry()
    {
        var cache = CreateCache(out var tempRoot);
        try
        {
            cache.Update(new LiveSessionSnapshot
            {
                Source = "fixture",
                Session = new LiveSessionInfo
                {
                    TrackName = "Loch Drummond - Short",
                    LapDistanceMeters = 1000.0,
                },
                Drivers =
                [
                    Driver("Setup1", progressPercent: 0.0, worldX: 0.0, worldZ: 0.0),
                    Driver("Setup2", progressPercent: 50.0, worldX: 100.0, worldZ: 200.0),
                    Driver("Setup3", progressPercent: 75.0, worldX: 50.0, worldZ: 300.0),
                ],
            });

            var geometry = cache.Get("Loch Drummond - Short");

            Assert.False(geometry.IsAvailable);
            Assert.False(geometry.IsCompleteLap);
            Assert.Equal(0, geometry.SampleCount);
            Assert.True(geometry.CoveragePercent < 90.0);
            Assert.Empty(geometry.Points);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// A complete lap produces resampled, closed geometry and is loaded by a fresh cache.
    /// </summary>
    [Fact]
    public void CompleteLapProducesPersistentClosedGeometry()
    {
        var cache = CreateCache(out var tempRoot);
        try
        {
            RecordFixtureLap(cache, "Setup1", completedLaps: 0, startIndex: 0, endIndex: 100);

            var geometry = cache.Get("Loch Drummond - Short");

            Assert.True(geometry.IsAvailable);
            Assert.True(geometry.IsCompleteLap);
            Assert.True(geometry.SampleCount >= 90);
            Assert.True(geometry.CoveragePercent >= 90.0);
            Assert.Equal(181, geometry.Points.Count);
            Assert.Equal(geometry.Points[0].X, geometry.Points[^1].X, precision: 8);
            Assert.Equal(geometry.Points[0].Y, geometry.Points[^1].Y, precision: 8);

            var reloaded = CreateCache(tempRoot).Get("Loch Drummond - Short");
            Assert.True(reloaded.IsAvailable);
            Assert.True(reloaded.SampleCount >= 90);
            Assert.True(reloaded.CoveragePercent >= 90.0);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Shared-memory geometry uses telemetry positions while scoring only supplies lap validity and progress context.
    /// </summary>
    [Fact]
    public void TelemetrySamplesProduceGeometryFromScoringContext()
    {
        var cache = CreateCache(out var tempRoot);
        try
        {
            RecordTelemetryLap(cache, "1", completedLaps: 0, startIndex: 0, endIndex: 100);

            var geometry = cache.Get("Loch Drummond - Short");

            Assert.True(geometry.IsAvailable);
            Assert.Equal("telemetry", geometry.Source);
            Assert.True(geometry.SampleCount >= 90);
            Assert.Contains(geometry.Points, point => Math.Abs(point.WorldX - 999.0) > 100.0);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Admin catalog shows seen tracks and can start a fresh improvement recording target.
    /// </summary>
    [Fact]
    public async Task CatalogListsSeenTracksAndStartsRecording()
    {
        var cache = CreateCache(out var tempRoot);
        try
        {
            cache.UpdateScoringSnapshot(new LiveSessionSnapshot
            {
                Source = "shared-memory",
                Session = new LiveSessionInfo { TrackName = "Loch Drummond - Short", LapDistanceMeters = 1000.0 },
                Drivers = [Driver("1", progressPercent: 0.0, worldX: 0.0, worldZ: 0.0)],
            });

            var seen = Assert.Single(cache.ListTracks());
            Assert.Equal("Loch Drummond - Short", seen.TrackName);
            Assert.True(seen.IsRecording);
            Assert.False(seen.HasGeometry);

            var recording = await cache.StartRecordingAsync(new TrackGeometryRecordingRequest
            {
                TrackName = "Loch Drummond - Short",
                TargetCompletedLaps = 3,
            }, CancellationToken.None);

            Assert.True(recording.IsRecording);
            Assert.Equal(3, recording.TargetCompletedLaps);
            Assert.Equal(0, recording.SampleCount);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Restarting a track recording resets the stored geometry and applies the new completed-lap target.
    /// </summary>
    [Fact]
    public async Task StartRecordingRestartsCompletedGeometryWithNewTarget()
    {
        var publisher = new RecordingLiveDataPublisher();
        var cache = CreateCache(out var tempRoot, publisher: publisher);
        try
        {
            RecordTelemetryLap(cache, "1", completedLaps: 0, startIndex: 0, endIndex: 100);
            Assert.True(cache.Get("Loch Drummond - Short").IsAvailable);

            var recording = await cache.StartRecordingAsync(new TrackGeometryRecordingRequest
            {
                TrackName = "Loch Drummond - Short",
                TargetCompletedLaps = 2,
            }, CancellationToken.None);

            Assert.True(recording.IsRecording);
            Assert.False(recording.HasGeometry);
            Assert.Equal(2, recording.TargetCompletedLaps);
            Assert.Equal(0, recording.RecordedLapCount);
            Assert.Equal(0, recording.SampleCount);
            var pushedGeometry = Assert.IsType<TrackGeometryChangedFrame>(publisher.PublishedFrames.Last()).Geometry;
            Assert.False(pushedGeometry.IsAvailable);
            Assert.Equal(0, pushedGeometry.SampleCount);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Improve mode keeps existing geometry and continues averaging until the new target completed-lap count is reached.
    /// </summary>
    [Fact]
    public async Task StartRecordingImprovesWithoutResettingGeometry()
    {
        var cache = CreateCache(out var tempRoot);
        try
        {
            RecordTelemetryLap(cache, "1", completedLaps: 0, startIndex: 0, endIndex: 100);
            var before = cache.Get("Loch Drummond - Short");
            Assert.True(before.IsAvailable);

            var recording = await cache.StartRecordingAsync(new TrackGeometryRecordingRequest
            {
                TrackName = "Loch Drummond - Short",
                TargetCompletedLaps = 2,
                ResetExistingGeometry = false,
            }, CancellationToken.None);

            Assert.True(recording.IsRecording);
            Assert.Equal(1, recording.RecordedLapCount);
            Assert.True(recording.SampleCount > 0);

            RecordTelemetryLap(cache, "1", completedLaps: 1, startIndex: 0, endIndex: 100);
            var after = Assert.Single(cache.ListTracks());
            Assert.False(after.IsRecording);
            Assert.Equal(2, after.RecordedLapCount);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Partial telemetry from several drivers must not count as completed geometry laps.
    /// </summary>
    [Fact]
    public void PartialTelemetryLapsDoNotCountAsCompletedLaps()
    {
        var cache = CreateCache(out var tempRoot, geometryRecordingLaps: 3);
        try
        {
            foreach (var driverId in new[] { "1", "2", "3" })
            {
                RecordTelemetryLap(cache, driverId, completedLaps: 0, startIndex: 0, endIndex: 40);
            }

            var track = Assert.Single(cache.ListTracks());
            var geometry = cache.Get("Loch Drummond - Short");

            Assert.True(track.IsRecording);
            Assert.Equal(0, track.RecordedLapCount);
            Assert.Equal(0, track.SampleCount);
            Assert.False(geometry.IsAvailable);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Complete telemetry laps from any driver contribute to the same averaged generated geometry.
    /// </summary>
    [Fact]
    public void CompleteTelemetryLapsFromMultipleDriversAreAveraged()
    {
        var cache = CreateCache(out var tempRoot, geometryRecordingLaps: 3);
        try
        {
            RecordTelemetryLap(cache, "1", completedLaps: 0, startIndex: 0, endIndex: 100, radiusOffset: 0.0);
            RecordTelemetryLap(cache, "2", completedLaps: 0, startIndex: 0, endIndex: 100, radiusOffset: 6.0);
            RecordTelemetryLap(cache, "1", completedLaps: 1, startIndex: 0, endIndex: 100, radiusOffset: -6.0);

            var track = Assert.Single(cache.ListTracks());
            var geometry = cache.Get("Loch Drummond - Short");

            Assert.False(track.IsRecording);
            Assert.True(track.HasGeometry);
            Assert.Equal(3, track.RecordedLapCount);
            Assert.True(geometry.IsAvailable);
            Assert.Equal("telemetry", geometry.Source);
            Assert.True(geometry.SampleCount >= 90);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Candidate telemetry laps that never receive a lap transition are expired from memory instead of being merged later.
    /// </summary>
    [Fact]
    public void StaleTelemetryCandidateLapDoesNotMergeAfterExpiry()
    {
        var timeProvider = new ManualTimeProvider(DateTimeOffset.Parse("2026-06-29T12:00:00Z"));
        var cache = CreateCache(out var tempRoot, timeProvider: timeProvider);
        try
        {
            RecordTelemetryLap(cache, "1", completedLaps: 0, startIndex: 0, endIndex: 100, emitLapTransition: false);
            timeProvider.Advance(TimeSpan.FromMinutes(11));
            RecordTelemetryLap(cache, "1", completedLaps: 1, startIndex: 0, endIndex: 0, emitLapTransition: false);

            var track = Assert.Single(cache.ListTracks());
            Assert.Equal(0, track.RecordedLapCount);
            Assert.Equal(0, track.SampleCount);
            Assert.False(cache.Get("Loch Drummond - Short").IsAvailable);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Pit, garage, invalid-lap, and off-track samples are rejected.
    /// </summary>
    [Fact]
    public void NonTrackSamplesAreRejected()
    {
        var cache = CreateCache(out var tempRoot);
        try
        {
            cache.Update(new LiveSessionSnapshot
            {
                Source = "fixture",
                Session = new LiveSessionInfo
                {
                    TrackName = "Loch Drummond - Short",
                    LapDistanceMeters = 1000.0,
                },
                Drivers =
                [
                    Driver("Pit", progressPercent: 10.0, worldX: 0.0, worldZ: 0.0) with { IsInPits = true },
                    Driver("Garage", progressPercent: 20.0, worldX: 0.0, worldZ: 0.0) with { IsInGarageStall = true },
                    Driver("Invalid", progressPercent: 30.0, worldX: 0.0, worldZ: 0.0) with { ValidLapFlag = 0 },
                    Driver("OffTrack", progressPercent: 40.0, worldX: 0.0, worldZ: 0.0) with { PathLateralMeters = 25.0, TrackEdgeMeters = 5.0 },
                ],
            });

            var geometry = cache.Get("Loch Drummond - Short");

            Assert.Equal(0, geometry.SampleCount);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static TrackGeometryRecorder CreateCache(
        out string tempRoot,
        int geometryRecordingLaps = TracksideDriverTrackerOptions.DefaultGeometryRecordingLaps,
        TimeProvider? timeProvider = null,
        ILiveDataPublisher? publisher = null)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), $"trackside-geometry-{Guid.NewGuid():N}");
        return CreateCache(tempRoot, geometryRecordingLaps, timeProvider, publisher);
    }

    private static TrackGeometryRecorder CreateCache(
        string tempRoot,
        int geometryRecordingLaps = TracksideDriverTrackerOptions.DefaultGeometryRecordingLaps,
        TimeProvider? timeProvider = null,
        ILiveDataPublisher? publisher = null) => new(
        timeProvider ?? TimeProvider.System,
        new TracksideRuntimeContext(true, false, tempRoot, null),
        new StaticOptionsMonitor<TracksideOptions>(new TracksideOptions
        {
            Deployment = new TracksideDeploymentOptions { DataPath = tempRoot },
            DriverTracker = new TracksideDriverTrackerOptions { GeometryRecordingLaps = geometryRecordingLaps },
        }),
        publisher ?? NoopLiveDataPublisher.Instance);

    private static void RecordTelemetryLap(
        TrackGeometryRecorder cache,
        string driverId,
        int completedLaps,
        int startIndex,
        int endIndex,
        double radiusOffset = 0.0,
        bool emitLapTransition = true)
    {
        for (var index = startIndex; index <= endIndex; index++)
        {
            var progress = index / 100.0;
            cache.UpdateScoringSnapshot(new LiveSessionSnapshot
            {
                Source = "shared-memory",
                Session = new LiveSessionInfo
                {
                    TrackName = "Loch Drummond - Short",
                    LapDistanceMeters = 1000.0,
                },
                Drivers = [Driver(driverId, progress * 100.0, worldX: 999.0, worldZ: 999.0) with { CompletedLaps = completedLaps }],
            });

            cache.UpdateTelemetry(new TelemetryPositionFrame
            {
                TrackName = "Loch Drummond - Short",
                Source = "telemetry",
                Vehicles =
                [
                    new TelemetryPositionVehicle
                    {
                        DriverId = driverId,
                        PosX = Math.Cos(progress * Math.Tau) * (100.0 + radiusOffset),
                        PosZ = Math.Sin(progress * Math.Tau) * (80.0 + radiusOffset),
                    },
                ],
            });
        }

        if (emitLapTransition)
        {
            // One sample of the next lap key marks the previous lap as completed for merge eligibility.
            cache.UpdateScoringSnapshot(new LiveSessionSnapshot
            {
                Source = "shared-memory",
                Session = new LiveSessionInfo
                {
                    TrackName = "Loch Drummond - Short",
                    LapDistanceMeters = 1000.0,
                },
                Drivers = [Driver(driverId, progressPercent: 0.0, worldX: 999.0, worldZ: 999.0) with { CompletedLaps = completedLaps + 1 }],
            });

            cache.UpdateTelemetry(new TelemetryPositionFrame
            {
                TrackName = "Loch Drummond - Short",
                Source = "telemetry",
                Vehicles =
                [
                    new TelemetryPositionVehicle
                    {
                        DriverId = driverId,
                        PosX = Math.Cos(0.0) * (100.0 + radiusOffset),
                        PosZ = Math.Sin(0.0) * (80.0 + radiusOffset),
                    },
                ],
            });
        }
    }

    private static void RecordFixtureLap(
        TrackGeometryRecorder cache,
        string driverId,
        int completedLaps,
        int startIndex,
        int endIndex,
        bool emitLapTransition = true)
    {
        for (var index = startIndex; index <= endIndex; index++)
        {
            var progress = index / 100.0;
            cache.Update(new LiveSessionSnapshot
            {
                Source = "fixture",
                Session = new LiveSessionInfo
                {
                    TrackName = "Loch Drummond - Short",
                    LapDistanceMeters = 1000.0,
                },
                Drivers = [Driver(driverId, progress * 100.0, Math.Cos(progress * Math.Tau) * 100.0, Math.Sin(progress * Math.Tau) * 80.0) with { CompletedLaps = completedLaps }],
            });
        }

        if (emitLapTransition)
        {
            cache.Update(new LiveSessionSnapshot
            {
                Source = "fixture",
                Session = new LiveSessionInfo
                {
                    TrackName = "Loch Drummond - Short",
                    LapDistanceMeters = 1000.0,
                },
                Drivers = [Driver(driverId, progressPercent: 0.0, worldX: 100.0, worldZ: 0.0) with { CompletedLaps = completedLaps + 1 }],
            });
        }
    }

    private static DriverSnapshot Driver(string rigName, double progressPercent, double worldX, double worldZ) => new()
    {
        DriverId = rigName,
        RigName = rigName,
        DisplayName = rigName,
        VehicleName = "Formula Pro",
        LeaderboardRank = 1,
        CompletedLaps = 0,
        ValidLapFlag = 2,
        TrackPositionPercent = progressPercent,
        PosX = worldX,
        PosZ = worldZ,
        PathLateralMeters = 0.0,
        TrackEdgeMeters = 6.0,
    };

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _value;

        public StaticOptionsMonitor(T value)
        {
            _value = value;
        }

        public T CurrentValue => _value;

        public T Get(string? name) => _value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        public ManualTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan value) => _utcNow += value;
    }

    private sealed class NoopLiveDataPublisher : ILiveDataPublisher
    {
        public static readonly NoopLiveDataPublisher Instance = new();

        public ValueTask PublishAsync<TFrame>(TFrame frame, CancellationToken cancellationToken)
            where TFrame : notnull => ValueTask.CompletedTask;
    }

    private sealed class RecordingLiveDataPublisher : ILiveDataPublisher
    {
        public List<object> PublishedFrames { get; } = [];

        public ValueTask PublishAsync<TFrame>(TFrame frame, CancellationToken cancellationToken)
            where TFrame : notnull
        {
            PublishedFrames.Add(frame);
            return ValueTask.CompletedTask;
        }
    }
}