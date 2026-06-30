using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Trackside.Application.LiveSession;
using Trackside.Application.Serialization;
using Trackside.Domain.LiveSession;
using Trackside.Service.Configuration;
using Trackside.Service.Hosting;

namespace Trackside.Service.Tracking;

/// <summary>
/// Live-data frame published when generated track geometry changes.
/// </summary>
public sealed record TrackGeometryChangedFrame
{
    /// <summary>
    /// Updated generated track geometry.
    /// </summary>
    public TrackGeometryResponse Geometry { get; init; } = TrackGeometryResponse.Unavailable(null, 0, 0.0, false, null, null);
}

/// <summary>
/// Builds, qualifies, and persists track geometry from telemetry world coordinates plus scoring context.
/// </summary>
public sealed class TrackGeometryRecorder : ILiveDataConsumer<ScoringContextFrame>, ILiveDataConsumer<TelemetryPositionFrame>
{
    private const int ProgressBinCount = 720;
    private const int CoverageBinCount = 100;
    private const int ResampledPointCount = 180;
    private const double MinimumCoveragePercent = 90.0;
    private const double TrackEdgeMarginMeters = 2.0;
    private static readonly TimeSpan PersistInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ScoringContextRetention = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CandidateLapRetention = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan TelemetryFallbackThreshold = TimeSpan.FromSeconds(2);

    private readonly object _gate = new();
    private readonly TimeProvider _timeProvider;
    private readonly TracksideRuntimeContext _runtimeContext;
    private readonly IOptionsMonitor<TracksideOptions> _options;
    private readonly ILiveDataPublisher _liveDataPublisher;
    private readonly Dictionary<string, TrackGeometryState> _tracks = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, DriverSampleContext>> _scoringContexts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _lastTelemetryUtcByTrack = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a geometry cache.
    /// </summary>
    public TrackGeometryRecorder(
        TimeProvider timeProvider,
        TracksideRuntimeContext runtimeContext,
        IOptionsMonitor<TracksideOptions> options,
        ILiveDataPublisher liveDataPublisher)
    {
        _timeProvider = timeProvider;
        _runtimeContext = runtimeContext;
        _options = options;
        _liveDataPublisher = liveDataPublisher;
    }

    /// <summary>
    /// Adds usable driver world-coordinate samples from a live snapshot.
    /// </summary>
    public void Update(LiveSessionSnapshot? snapshot) => UpdateScoringSnapshot(snapshot);

    /// <inheritdoc />
    public ValueTask ConsumeAsync(ScoringContextFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var changed = UpdateScoringSnapshotCore(frame.Snapshot);
        if (changed is not null)
        {
            return _liveDataPublisher.PublishAsync(changed, cancellationToken);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask ConsumeAsync(TelemetryPositionFrame frame, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var changed = UpdateTelemetryCore(frame);
        if (changed is not null)
        {
            return _liveDataPublisher.PublishAsync(changed, cancellationToken);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void UpdateScoringSnapshot(LiveSessionSnapshot? snapshot) => UpdateScoringSnapshotCore(snapshot);

    private TrackGeometryChangedFrame? UpdateScoringSnapshotCore(LiveSessionSnapshot? snapshot)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.Session.TrackName))
        {
            return null;
        }

        if (!IsTrackNameUsable(snapshot.Session.TrackName))
        {
            return null;
        }

        lock (_gate)
        {
            var trackName = snapshot.Session.TrackName.Trim();
            var state = GetOrLoadState(trackName);
            var now = _timeProvider.GetUtcNow();
            PruneVolatileState(now);
            if (state.SeenUtc == default)
            {
                state.SeenUtc = now;
            }

            var contexts = new Dictionary<string, DriverSampleContext>(StringComparer.OrdinalIgnoreCase);
            var changed = false;
            var useScoringPositions = ShouldUseScoringPositions(snapshot.Source, trackName, now);
            foreach (var driver in snapshot.Drivers)
            {
                var context = TryCreateSampleContext(driver, snapshot.Session, now);
                if (context is null)
                {
                    continue;
                }

                var sampleContext = context.Value;
                contexts[driver.DriverId] = sampleContext;
                if (useScoringPositions && IsFinite(driver.PosX) && IsFinite(driver.PosZ))
                {
                    // Fixture/recorded snapshots have no telemetry frame, and live scoring is the fallback when telemetry is unavailable.
                    changed |= state.Add(new WorldSample(driver.PosX!.Value, driver.PosZ!.Value, sampleContext.ProgressFraction, sampleContext.LapKey, 1), now);
                }
            }

            _scoringContexts[trackName] = contexts;

            if (!changed && state.UpdatedUtc != default)
            {
                return null;
            }

            state.Source = snapshot.Source;
            state.UpdatedUtc = now;
            var wasComplete = state.IsCompleteLap;
            state.RecalculateQuality();
            if (state.LastPersistedUtc is null || now - state.LastPersistedUtc >= PersistInterval || (!wasComplete && state.IsCompleteLap))
            {
                Persist(state, now);
            }

            return new TrackGeometryChangedFrame { Geometry = BuildResponse(state) };
        }
    }

    /// <inheritdoc />
    public void UpdateTelemetry(TelemetryPositionFrame? frame) => UpdateTelemetryCore(frame);

    private TrackGeometryChangedFrame? UpdateTelemetryCore(TelemetryPositionFrame? frame)
    {
        if (frame is null || string.IsNullOrWhiteSpace(frame.TrackName) || frame.Vehicles.Count == 0)
        {
            return null;
        }

        if (!IsTrackNameUsable(frame.TrackName))
        {
            return null;
        }

        lock (_gate)
        {
            var trackName = frame.TrackName.Trim();
            var state = GetOrLoadState(trackName);
            var now = _timeProvider.GetUtcNow();
            _lastTelemetryUtcByTrack[trackName] = now;
            PruneVolatileState(now);
            if (state.SeenUtc == default)
            {
                state.SeenUtc = now;
            }

            if (!_scoringContexts.TryGetValue(trackName, out var contexts))
            {
                state.Source = frame.Source;
                state.UpdatedUtc = now;
                return null;
            }

            var changed = false;
            foreach (var vehicle in frame.Vehicles)
            {
                if (!contexts.TryGetValue(vehicle.DriverId, out var context) || now - context.UpdatedUtc > ScoringContextRetention || !IsFinite(vehicle.PosX) || !IsFinite(vehicle.PosZ))
                {
                    continue;
                }

                changed |= state.Add(new WorldSample(vehicle.PosX, vehicle.PosZ, context.ProgressFraction, context.LapKey, 1), now);
            }

            if (!changed)
            {
                return null;
            }

            state.Source = frame.Source;
            state.UpdatedUtc = now;
            var wasComplete = state.IsCompleteLap;
            state.RecalculateQuality();
            if (state.LastPersistedUtc is null || now - state.LastPersistedUtc >= PersistInterval || (!wasComplete && state.IsCompleteLap))
            {
                Persist(state, now);
            }

            return new TrackGeometryChangedFrame { Geometry = BuildResponse(state) };
        }
    }

    /// <summary>
    /// Returns cached geometry for a track, if enough ordered world-coordinate samples exist.
    /// </summary>
    public TrackGeometryResponse Get(string? trackName)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return TrackGeometryResponse.Unavailable(trackName, 0, 0.0, false, null, null);
        }

        lock (_gate)
        {
            return BuildResponse(GetOrLoadState(trackName.Trim()));
        }
    }

    /// <summary>
    /// Returns all seen or persisted track-geometry recording states.
    /// </summary>
    public IReadOnlyList<TrackGeometryCatalogEntry> ListTracks()
    {
        lock (_gate)
        {
            LoadPersistedStates();
            return _tracks.Values
                .OrderBy(state => state.TrackName, StringComparer.OrdinalIgnoreCase)
                .Select(ToCatalogEntry)
                .ToList();
        }
    }

    /// <summary>
    /// Starts a new telemetry recording pass for a seen or named track.
    /// </summary>
    public async ValueTask<TrackGeometryCatalogEntry> StartRecordingAsync(TrackGeometryRecordingRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.TrackName);
        TrackGeometryChangedFrame changed;
        TrackGeometryCatalogEntry entry;

        lock (_gate)
        {
            var state = GetOrLoadState(request.TrackName.Trim());
            var now = _timeProvider.GetUtcNow();
            if (state.SeenUtc == default)
            {
                state.SeenUtc = now;
            }

            state.StartRecording(ClampTargetLaps(request.TargetCompletedLaps ?? DefaultTargetLaps()), request.ResetExistingGeometry);
            state.UpdatedUtc = now;
            Persist(state, now);
            entry = ToCatalogEntry(state);
            changed = new TrackGeometryChangedFrame { Geometry = BuildResponse(state) };
        }

        await _liveDataPublisher.PublishAsync(changed, cancellationToken);
        return entry;
    }

    private TrackGeometryState GetOrLoadState(string trackName)
    {
        if (_tracks.TryGetValue(trackName, out var cached))
        {
            return cached;
        }

        var state = Load(trackName) ?? new TrackGeometryState(trackName, DefaultTargetLaps());
        _tracks[trackName] = state;
        return state;
    }

    private TrackGeometryCatalogEntry ToCatalogEntry(TrackGeometryState state) => new()
    {
        TrackName = state.TrackName,
        Source = state.Source,
        SeenUtc = state.SeenUtc,
        UpdatedUtc = state.UpdatedUtc,
        LastPersistedUtc = state.LastPersistedUtc,
        HasGeometry = state.IsCompleteLap,
        IsRecording = state.IsRecording,
        IsImprovementRecording = state.RecordingRequested,
        CoveragePercent = state.CoveragePercent,
        SampleCount = state.Samples.Count,
        TargetCompletedLaps = state.TargetCompletedLaps,
        RecordedLapCount = state.RecordedLapCount,
    };

    private int DefaultTargetLaps() => ClampTargetLaps(_options.CurrentValue.DriverTracker.GeometryRecordingLaps);

    private static int ClampTargetLaps(int value) => Math.Clamp(
        value,
        TracksideDriverTrackerOptions.MinimumGeometryRecordingLaps,
        TracksideDriverTrackerOptions.MaximumGeometryRecordingLaps);

    private void LoadPersistedStates()
    {
        var root = GeometryRoot();
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(root, "*.json"))
        {
            var state = LoadFile(path);
            if (state is null || _tracks.ContainsKey(state.TrackName))
            {
                continue;
            }

            _tracks[state.TrackName] = state;
        }
    }

    private TrackGeometryState? Load(string trackName)
    {
        var path = GeometryFilePath(trackName);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var state = LoadFile(path);
            if (state is null || !string.Equals(state.TrackName, trackName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
            return state;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private TrackGeometryState? LoadFile(string path)
    {
        try
        {
            var persisted = JsonSerializer.Deserialize<PersistedTrackGeometry>(File.ReadAllText(path), TracksideJson.SerializerOptions);
            if (persisted is null || string.IsNullOrWhiteSpace(persisted.TrackName))
            {
                return null;
            }

            var persistedTargetLaps = persisted.TargetCompletedLaps ?? (persisted.IsCompleteLap ? 1 : DefaultTargetLaps());
            var state = new TrackGeometryState(persisted.TrackName, ClampTargetLaps(persistedTargetLaps))
            {
                Source = persisted.Source,
                SeenUtc = persisted.SeenUtc == default ? persisted.UpdatedUtc : persisted.SeenUtc,
                UpdatedUtc = persisted.UpdatedUtc,
                LastPersistedUtc = persisted.LastPersistedUtc,
                RecordingRequested = persisted.RecordingRequested,
            };

            foreach (var lapKey in persisted.RecordedLapKeys)
            {
                if (!string.IsNullOrWhiteSpace(lapKey))
                {
                    state.RecordedLapKeys.Add(lapKey);
                }
            }

            foreach (var sample in persisted.Samples)
            {
                if (!string.IsNullOrWhiteSpace(sample.LapKey))
                {
                    state.RecordedLapKeys.Add(sample.LapKey);
                }

                state.Samples[sample.Bin] = new WorldSample(
                    sample.WorldX,
                    sample.WorldZ,
                    sample.ProgressFraction,
                    sample.LapKey ?? string.Empty,
                    sample.Count);
            }

            if (persisted.IsCompleteLap && state.RecordedLapKeys.Count == 0)
            {
                state.RecordedLapKeys.Add("legacy:1");
            }

            state.RecalculateQuality();
            return state;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private void Persist(TrackGeometryState state, DateTimeOffset now)
    {
        var path = GeometryFilePath(state.TrackName);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? GeometryRoot());
        state.LastPersistedUtc = now;
        var persisted = new PersistedTrackGeometry
        {
            TrackName = state.TrackName,
            Source = state.Source,
            UpdatedUtc = state.UpdatedUtc,
            SeenUtc = state.SeenUtc,
            LastPersistedUtc = state.LastPersistedUtc,
            CoveragePercent = state.CoveragePercent,
            IsCompleteLap = state.IsCompleteLap,
            RecordingRequested = state.RecordingRequested,
            TargetCompletedLaps = state.TargetCompletedLaps,
            RecordedLapKeys = state.RecordedLapKeys.OrderBy(key => key, StringComparer.Ordinal).ToList(),
            Samples = state.Samples.Select(pair => new PersistedWorldSample
            {
                Bin = pair.Key,
                WorldX = pair.Value.WorldX,
                WorldZ = pair.Value.WorldZ,
                ProgressFraction = pair.Value.ProgressFraction,
                LapKey = pair.Value.LapKey,
                Count = pair.Value.Count,
            }).ToList(),
        };

        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        var options = new JsonSerializerOptions(TracksideJson.SerializerOptions) { WriteIndented = true };
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(persisted, options));
        File.Move(temporaryPath, path, overwrite: true);
    }

    private TrackGeometryResponse BuildResponse(TrackGeometryState state)
    {
        var samples = state.Samples.Values
            .OrderBy(sample => sample.ProgressFraction)
            .ToList();

        if (!state.IsCompleteLap || samples.Count < 4)
        {
            return TrackGeometryResponse.Unavailable(state.TrackName, samples.Count, state.CoveragePercent, state.IsCompleteLap, state.UpdatedUtc, state.Source);
        }

        var resampled = SmoothCircular(Resample(samples));
        if (resampled.Count == 0)
        {
            return TrackGeometryResponse.Unavailable(state.TrackName, samples.Count, state.CoveragePercent, state.IsCompleteLap, state.UpdatedUtc, state.Source);
        }

        var closed = resampled.Concat([resampled[0]]).ToList();
        var minWorldX = closed.Min(sample => sample.WorldX);
        var maxWorldX = closed.Max(sample => sample.WorldX);
        var minWorldZ = closed.Min(sample => sample.WorldZ);
        var maxWorldZ = closed.Max(sample => sample.WorldZ);
        var width = maxWorldX - minWorldX;
        var height = maxWorldZ - minWorldZ;
        var bounds = new TrackGeometryBounds
        {
            MinWorldX = minWorldX,
            MaxWorldX = maxWorldX,
            MinWorldZ = minWorldZ,
            MaxWorldZ = maxWorldZ,
        };

        var points = closed.Select(sample => new TrackGeometryPoint
        {
            WorldX = sample.WorldX,
            WorldZ = sample.WorldZ,
            X = Normalize(sample.WorldX, minWorldX, width),
            Y = Normalize(maxWorldZ - sample.WorldZ, 0.0, height),
            ProgressPercent = Math.Round(sample.ProgressFraction * 100.0, 2),
        }).ToList();

        return new TrackGeometryResponse
        {
            IsAvailable = true,
            TrackName = state.TrackName,
            Source = state.Source,
            UpdatedUtc = state.UpdatedUtc,
            SampleCount = samples.Count,
            CoveragePercent = state.CoveragePercent,
            IsCompleteLap = state.IsCompleteLap,
            Bounds = bounds,
            Points = points,
        };
    }

    private static List<WorldSample> Resample(IReadOnlyList<WorldSample> samples)
    {
        if (samples.Count == 0)
        {
            return [];
        }

        var anchors = samples
            .OrderBy(sample => sample.ProgressFraction)
            .ToList();
        anchors.Add(anchors[0] with { ProgressFraction = 1.0 });

        var result = new List<WorldSample>(ResampledPointCount);
        var segmentIndex = 0;
        for (var index = 0; index < ResampledPointCount; index++)
        {
            var target = (double)index / ResampledPointCount;
            while (segmentIndex < anchors.Count - 2 && anchors[segmentIndex + 1].ProgressFraction < target)
            {
                segmentIndex++;
            }

            var start = anchors[segmentIndex];
            var end = anchors[Math.Min(segmentIndex + 1, anchors.Count - 1)];
            var span = Math.Max(0.000001, end.ProgressFraction - start.ProgressFraction);
            var factor = Math.Clamp((target - start.ProgressFraction) / span, 0.0, 1.0);
            result.Add(new WorldSample(
                Lerp(start.WorldX, end.WorldX, factor),
                Lerp(start.WorldZ, end.WorldZ, factor),
                target,
                start.LapKey,
                1));
        }

        return result;
    }

    private static List<WorldSample> SmoothCircular(IReadOnlyList<WorldSample> samples)
    {
        if (samples.Count < 3)
        {
            return samples.ToList();
        }

        var result = new List<WorldSample>(samples.Count);
        for (var index = 0; index < samples.Count; index++)
        {
            var previous = samples[(index - 1 + samples.Count) % samples.Count];
            var current = samples[index];
            var next = samples[(index + 1) % samples.Count];
            result.Add(new WorldSample(
                (previous.WorldX + (current.WorldX * 2.0) + next.WorldX) / 4.0,
                (previous.WorldZ + (current.WorldZ * 2.0) + next.WorldZ) / 4.0,
                current.ProgressFraction,
                current.LapKey,
                current.Count));
        }

        return result;
    }

    private void PruneVolatileState(DateTimeOffset now)
    {
        foreach (var trackContexts in _scoringContexts.Values)
        {
            foreach (var pair in trackContexts.Where(pair => now - pair.Value.UpdatedUtc > ScoringContextRetention).ToList())
            {
                trackContexts.Remove(pair.Key);
            }
        }

        foreach (var state in _tracks.Values)
        {
            state.PruneVolatileState(now, CandidateLapRetention);
        }

        foreach (var pair in _lastTelemetryUtcByTrack.Where(pair => now - pair.Value > ScoringContextRetention).ToList())
        {
            _lastTelemetryUtcByTrack.Remove(pair.Key);
        }
    }

    private bool ShouldUseScoringPositions(string source, string trackName, DateTimeOffset now)
    {
        if (!string.Equals(source, "shared-memory", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !_lastTelemetryUtcByTrack.TryGetValue(trackName, out var lastTelemetryUtc)
            || now - lastTelemetryUtc > TelemetryFallbackThreshold;
    }

    private static DriverSampleContext? TryCreateSampleContext(DriverSnapshot driver, LiveSessionInfo session, DateTimeOffset updatedUtc)
    {
        if (driver.IsInPits || driver.IsInGarageStall || driver.ValidLapFlag == 0 || !IsOnTrack(driver))
        {
            return null;
        }

        var progressFraction = ResolveProgressFraction(driver, session);
        if (progressFraction is null)
        {
            return null;
        }

        return new DriverSampleContext(progressFraction.Value, $"{driver.DriverId}:{driver.CompletedLaps + 1}", updatedUtc);
    }

    private static bool IsOnTrack(DriverSnapshot driver)
    {
        if (!IsFinite(driver.PathLateralMeters) || !IsFinite(driver.TrackEdgeMeters) || driver.TrackEdgeMeters <= 0)
        {
            return true;
        }

        var pathLateralMeters = driver.PathLateralMeters.GetValueOrDefault();
        var trackEdgeMeters = driver.TrackEdgeMeters.GetValueOrDefault();
        return Math.Abs(pathLateralMeters) <= trackEdgeMeters + TrackEdgeMarginMeters;
    }

    private static double? ResolveProgressFraction(DriverSnapshot driver, LiveSessionInfo session)
    {
        if (IsFinite(driver.TrackPositionPercent))
        {
            return Math.Clamp(driver.TrackPositionPercent!.Value / 100.0, 0.0, 1.0);
        }

        if (IsFinite(driver.LapDistanceMeters) && IsFinite(session.LapDistanceMeters) && session.LapDistanceMeters > 0)
        {
            return Math.Clamp(driver.LapDistanceMeters!.Value / session.LapDistanceMeters!.Value, 0.0, 1.0);
        }

        return null;
    }

    private string GeometryFilePath(string trackName) => Path.Combine(GeometryRoot(), $"{SanitizeFileName(trackName)}-{HashTrackName(trackName)}.json");

    private string GeometryRoot()
    {
        var dataRoot = ResolvePath(_options.CurrentValue.Deployment.DataPath ?? Path.Combine(_runtimeContext.ContentRootPath, "App_Data"), _runtimeContext.ContentRootPath);
        return Path.Combine(dataRoot, "track-geometry");
    }

    private static string ResolvePath(string path, string basePath) => Path.IsPathRooted(path)
        ? Path.GetFullPath(path)
        : Path.GetFullPath(Path.Combine(basePath, path));

    private static string SanitizeFileName(string trackName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(trackName.Length);
        foreach (var character in trackName)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.ToString().Trim().Replace(' ', '-');
    }

    private static string HashTrackName(string trackName)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(trackName.ToUpperInvariant()));
        return Convert.ToHexString(hash, 0, 4).ToLowerInvariant();
    }

    private static double Normalize(double value, double min, double span) => span <= double.Epsilon
        ? 0.5
        : Math.Clamp((value - min) / span, 0.0, 1.0);

    private static double Lerp(double start, double end, double factor) => start + ((end - start) * factor);

    private static bool IsFinite(double? value) => value is not null && double.IsFinite(value.Value);

    private static bool IsTrackNameUsable(string? trackName) => !string.IsNullOrWhiteSpace(trackName)
        && !string.Equals(trackName.Trim(), "Unknown track", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(trackName.Trim(), "No live scoring source", StringComparison.OrdinalIgnoreCase);

    private sealed class TrackGeometryState
    {
        public TrackGeometryState(string trackName, int targetCompletedLaps)
        {
            TrackName = trackName;
            TargetCompletedLaps = targetCompletedLaps;
        }

        public string TrackName { get; }

        public string? Source { get; set; }

        public DateTimeOffset SeenUtc { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }

        public DateTimeOffset? LastPersistedUtc { get; set; }

        public double CoveragePercent { get; private set; }

        public bool IsCompleteLap { get; private set; }

        public bool RecordingRequested { get; set; }

        public int TargetCompletedLaps { get; private set; }

        public int RecordedLapCount => RecordedLapKeys.Count;

        public bool IsRecording => !IsCompleteLap || RecordingRequested;

        public SortedDictionary<int, WorldSample> Samples { get; } = [];

        public HashSet<string> RecordedLapKeys { get; } = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, LapSampleAccumulator> CandidateLaps { get; } = new(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ActiveDriverLap> ActiveDriverLaps { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void StartRecording(int targetCompletedLaps, bool resetExistingGeometry)
        {
            TargetCompletedLaps = targetCompletedLaps;
            RecordingRequested = true;
            if (resetExistingGeometry)
            {
                Samples.Clear();
                RecordedLapKeys.Clear();
                CandidateLaps.Clear();
                ActiveDriverLaps.Clear();
                IsCompleteLap = false;
                CoveragePercent = 0.0;
                return;
            }

            RecalculateQuality();
        }

        public bool Add(WorldSample sample, DateTimeOffset now)
        {
            if (!IsRecording || string.IsNullOrWhiteSpace(sample.LapKey))
            {
                return false;
            }

            if (RecordedLapKeys.Contains(sample.LapKey))
            {
                return false;
            }

            var driverId = DriverIdFromLapKey(sample.LapKey);
            if (string.IsNullOrWhiteSpace(driverId))
            {
                return false;
            }

            var changed = false;
            if (ActiveDriverLaps.TryGetValue(driverId, out var activeLap) && !string.Equals(activeLap.LapKey, sample.LapKey, StringComparison.OrdinalIgnoreCase))
            {
                // A lap only counts after the driver actually transitions into the next lap key.
                changed |= FinalizeCandidateLap(activeLap.LapKey);
            }

            ActiveDriverLaps[driverId] = new ActiveDriverLap(sample.LapKey, now);

            var candidate = GetCandidateLap(sample.LapKey);
            candidate.Add(sample, now);
            return changed;
        }

        public void PruneVolatileState(DateTimeOffset now, TimeSpan candidateRetention)
        {
            foreach (var pair in CandidateLaps.Where(pair => now - pair.Value.LastUpdatedUtc > candidateRetention).ToList())
            {
                CandidateLaps.Remove(pair.Key);
            }

            foreach (var pair in ActiveDriverLaps.Where(pair => now - pair.Value.LastSeenUtc > candidateRetention).ToList())
            {
                ActiveDriverLaps.Remove(pair.Key);
            }
        }

        private LapSampleAccumulator GetCandidateLap(string lapKey)
        {
            if (CandidateLaps.TryGetValue(lapKey, out var candidate))
            {
                return candidate;
            }

            candidate = new LapSampleAccumulator(lapKey);
            CandidateLaps[lapKey] = candidate;
            return candidate;
        }

        private bool FinalizeCandidateLap(string lapKey)
        {
            if (!CandidateLaps.Remove(lapKey, out var lap) || !lap.IsCompleteLap || !RecordedLapKeys.Add(lap.LapKey))
            {
                return false;
            }

            // Keep every completed qualifying lap and blend bin-by-bin so geometry improves across drivers/laps.
            foreach (var pair in lap.Samples)
            {
                Samples[pair.Key] = Samples.TryGetValue(pair.Key, out var existing)
                    ? existing.Blend(pair.Value)
                    : pair.Value;
            }

            return true;
        }

        private static string DriverIdFromLapKey(string lapKey)
        {
            var separatorIndex = lapKey.IndexOf(':');
            return separatorIndex <= 0 ? string.Empty : lapKey[..separatorIndex];
        }

        public void RecalculateQuality()
        {
            var quality = CalculateQuality(Samples.Values);
            CoveragePercent = quality.CoveragePercent;
            IsCompleteLap = quality.HasCompleteCoverage && RecordedLapCount >= TargetCompletedLaps;
            if (IsCompleteLap)
            {
                RecordingRequested = false;
            }
        }
    }

    private sealed class LapSampleAccumulator
    {
        public LapSampleAccumulator(string lapKey)
        {
            LapKey = lapKey;
        }

        public string LapKey { get; }

        public bool IsCompleteLap { get; private set; }

        public DateTimeOffset LastUpdatedUtc { get; private set; }

        public SortedDictionary<int, WorldSample> Samples { get; } = [];

        public void Add(WorldSample sample, DateTimeOffset now)
        {
            LastUpdatedUtc = now;
            var bin = ProgressBin(sample.ProgressFraction);
            Samples[bin] = Samples.TryGetValue(bin, out var existing)
                ? existing.Blend(sample)
                : sample;
            IsCompleteLap = CalculateQuality(Samples.Values).HasCompleteCoverage;
        }
    }

    private static TrackCoverageQuality CalculateQuality(IEnumerable<WorldSample> samples)
    {
        var values = samples as IReadOnlyCollection<WorldSample> ?? samples.ToList();
        if (values.Count == 0)
        {
            return new TrackCoverageQuality(0.0, false);
        }

        var coverageBins = new HashSet<int>();
        foreach (var sample in values)
        {
            coverageBins.Add((int)Math.Floor(Math.Clamp(sample.ProgressFraction, 0.0, 0.999999) * CoverageBinCount));
        }

        var coveragePercent = Math.Round(coverageBins.Count / (double)CoverageBinCount * 100.0, 2);
        var hasStart = values.Any(sample => sample.ProgressFraction <= 0.03);
        var hasEnd = values.Any(sample => sample.ProgressFraction >= 0.97);
        return new TrackCoverageQuality(coveragePercent, coveragePercent >= MinimumCoveragePercent && hasStart && hasEnd);
    }

    private static int ProgressBin(double progressFraction) => (int)Math.Round(Math.Clamp(progressFraction, 0.0, 1.0) * (ProgressBinCount - 1));

    private readonly record struct TrackCoverageQuality(double CoveragePercent, bool HasCompleteCoverage);

    private readonly record struct DriverSampleContext(double ProgressFraction, string LapKey, DateTimeOffset UpdatedUtc);

    private readonly record struct ActiveDriverLap(string LapKey, DateTimeOffset LastSeenUtc);

    private readonly record struct WorldSample(double WorldX, double WorldZ, double ProgressFraction, string LapKey, int Count)
    {
        public WorldSample Blend(WorldSample next)
        {
            var nextCount = Count + next.Count;
            return new WorldSample(
                ((WorldX * Count) + (next.WorldX * next.Count)) / nextCount,
                ((WorldZ * Count) + (next.WorldZ * next.Count)) / nextCount,
                ProgressFraction,
                LapKey,
                nextCount);
        }
    }

    private sealed record PersistedTrackGeometry
    {
        public string TrackName { get; init; } = string.Empty;

        public string? Source { get; init; }

        public DateTimeOffset UpdatedUtc { get; init; }

        public DateTimeOffset SeenUtc { get; init; }

        public DateTimeOffset? LastPersistedUtc { get; init; }

        public double CoveragePercent { get; init; }

        public bool IsCompleteLap { get; init; }

        public bool RecordingRequested { get; init; }

        public int? TargetCompletedLaps { get; init; }

        public List<string> RecordedLapKeys { get; init; } = [];

        public List<PersistedWorldSample> Samples { get; init; } = [];
    }

    private sealed record PersistedWorldSample
    {
        public int Bin { get; init; }

        public double WorldX { get; init; }
        public double WorldZ { get; init; }
        public double ProgressFraction { get; init; }
        public string? LapKey { get; init; }
        public int Count { get; init; }
    }
}

/// <summary>
/// One track entry in the admin driver-tracker geometry catalog.
/// </summary>
public sealed record TrackGeometryCatalogEntry
{
    /// <summary>
    /// Track name reported by rFactor 2.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Last geometry source for this track.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// First time this service saw the track.
    /// </summary>
    public DateTimeOffset SeenUtc { get; init; }

    /// <summary>
    /// Last time geometry state changed.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; init; }

    /// <summary>
    /// Last time geometry state was persisted.
    /// </summary>
    public DateTimeOffset? LastPersistedUtc { get; init; }

    /// <summary>
    /// True when generated geometry is available for this track.
    /// </summary>
    public bool HasGeometry { get; init; }

    /// <summary>
    /// True when the telemetry recorder will accept samples for this track.
    /// </summary>
    public bool IsRecording { get; init; }

    /// <summary>
    /// True when recording was manually requested to improve existing geometry.
    /// </summary>
    public bool IsImprovementRecording { get; init; }

    /// <summary>
    /// Progress coverage across the track lap.
    /// </summary>
    public double CoveragePercent { get; init; }

    /// <summary>
    /// Number of progress-bin samples currently stored.
    /// </summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// Complete lap passes requested before geometry is considered complete.
    /// </summary>
    public int TargetCompletedLaps { get; init; }

    /// <summary>
    /// Distinct lap passes contributing samples to the current geometry.
    /// </summary>
    public int RecordedLapCount { get; init; }
}

/// <summary>
/// Request used to start or improve generated track geometry.
/// </summary>
public sealed record TrackGeometryRecordingRequest
{
    /// <summary>
    /// Track to record.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Optional complete lap target for this recording pass.
    /// </summary>
    public int? TargetCompletedLaps { get; init; }

    /// <summary>
    /// True to replace existing generated geometry before recording.
    /// </summary>
    public bool ResetExistingGeometry { get; init; } = true;
}

/// <summary>
/// Public track geometry response used by the driver tracker page.
/// </summary>
public sealed record TrackGeometryResponse
{
    /// <summary>
    /// True when enough on-track samples from a complete lap are available to draw a reliable track line.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Track name associated with this geometry.
    /// </summary>
    public string? TrackName { get; init; }

    /// <summary>
    /// Source that last contributed samples.
    /// </summary>
    public string? Source { get; init; }

    /// <summary>
    /// Last cache update timestamp.
    /// </summary>
    public DateTimeOffset? UpdatedUtc { get; init; }

    /// <summary>
    /// Number of ordered geometry samples in the cache.
    /// </summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// Approximate lap coverage represented by sample bins.
    /// </summary>
    public double CoveragePercent { get; init; }

    /// <summary>
    /// True when coverage includes enough start-to-finish samples for reliable rendering.
    /// </summary>
    public bool IsCompleteLap { get; init; }

    /// <summary>
    /// Raw world-coordinate bounds used for frontend driver marker normalization.
    /// </summary>
    public TrackGeometryBounds? Bounds { get; init; }

    /// <summary>
    /// Normalized, resampled, smoothed, and closed geometry points ordered by lap progress.
    /// </summary>
    public IReadOnlyList<TrackGeometryPoint> Points { get; init; } = [];

    /// <summary>
    /// Creates an unavailable response for missing or under-sampled geometry.
    /// </summary>
    public static TrackGeometryResponse Unavailable(string? trackName, int sampleCount, double coveragePercent, bool isCompleteLap, DateTimeOffset? updatedUtc, string? source) => new()
    {
        TrackName = trackName,
        Source = source,
        UpdatedUtc = updatedUtc,
        SampleCount = sampleCount,
        CoveragePercent = coveragePercent,
        IsCompleteLap = isCompleteLap,
    };
}

/// <summary>
/// Raw X/Z world-coordinate bounds for a generated track geometry.
/// </summary>
public sealed record TrackGeometryBounds
{
    /// <summary>Minimum raw world X coordinate.</summary>
    public double MinWorldX { get; init; }

    /// <summary>Maximum raw world X coordinate.</summary>
    public double MaxWorldX { get; init; }

    /// <summary>Minimum raw world Z coordinate.</summary>
    public double MinWorldZ { get; init; }

    /// <summary>Maximum raw world Z coordinate.</summary>
    public double MaxWorldZ { get; init; }
}

/// <summary>
/// One normalized point in a generated track geometry.
/// </summary>
public sealed record TrackGeometryPoint
{
    /// <summary>Raw world X coordinate in meters.</summary>
    public double WorldX { get; init; }

    /// <summary>Raw world Z coordinate in meters.</summary>
    public double WorldZ { get; init; }

    /// <summary>Normalized horizontal coordinate, from 0 to 1.</summary>
    public double X { get; init; }

    /// <summary>Normalized vertical coordinate, from 0 to 1, with world Z inverted for screen rendering.</summary>
    public double Y { get; init; }

    /// <summary>Lap progress percentage associated with this sample.</summary>
    public double ProgressPercent { get; init; }
}