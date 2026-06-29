using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Application.Persistence;
using Trackside.Domain.LiveSession;
using Trackside.Infrastructure.Rf2.SharedMemory;

namespace Trackside.Infrastructure.LiveSession.SharedMemory;

/// <summary>
/// Reads rFactor 2 scoring data through a dedicated shared-memory polling loop.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SharedMemoryLiveSessionSource : ILiveSessionSource, IDisposable
{
    private readonly IOptionsMonitor<TracksideSourceOptions> _sourceOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILeaderboardSnapshotBuilder _snapshotBuilder;
    private readonly ITracksideStore _store;
    private readonly IRf2ScoringPayloadParser _parser;
    private readonly IRf2TelemetryPayloadParser _telemetryParser;
    private readonly ILiveDataPublisher _liveDataPublisher;
    private readonly Rf2SharedMemoryMapReader _mapReader;
    private readonly IRf2SharedMemoryMapDiscovery _mapDiscovery;
    private readonly Rf2ScoringMapResolver _mapResolver;
    private readonly MappedBufferPayloadLocator _payloadLocator;
    private readonly SharedMemoryPollingLoop<ScoringLoopValue> _scoringLoop;
    private readonly SharedMemoryPollingLoop<TelemetryLoopValue> _telemetryLoop;
    private long _updateSequence;

    /// <summary>
    /// Creates the shared-memory live-session source.
    /// </summary>
    /// <param name="sourceOptions">Source configuration.</param>
    /// <param name="timeProvider">Clock used for publication timestamps.</param>
    /// <param name="snapshotBuilder">Raw-to-normalized leaderboard builder.</param>
    /// <param name="store">Durable Phase 2 store used for staff aliases when enabled.</param>
    /// <param name="parser">rF2 scoring payload parser.</param>
    /// <param name="telemetryParser">rF2 telemetry payload parser.</param>
    /// <param name="liveDataPublisher">Publisher for projected live-data frames.</param>
    /// <param name="mapReader">Shared-memory map reader.</param>
    /// <param name="mapDiscovery">Shared-memory map discovery service.</param>
    /// <param name="mapResolver">Map resolution and ambiguity policy service.</param>
    /// <param name="payloadLocator">Mapped-buffer offset locator.</param>
    /// <param name="logger">Logger for source diagnostics.</param>
    public SharedMemoryLiveSessionSource(
        IOptionsMonitor<TracksideSourceOptions> sourceOptions,
        TimeProvider timeProvider,
        ILeaderboardSnapshotBuilder snapshotBuilder,
        ITracksideStore store,
        IRf2ScoringPayloadParser parser,
        IRf2TelemetryPayloadParser telemetryParser,
        ILiveDataPublisher liveDataPublisher,
        Rf2SharedMemoryMapReader mapReader,
        IRf2SharedMemoryMapDiscovery mapDiscovery,
        Rf2ScoringMapResolver mapResolver,
        MappedBufferPayloadLocator payloadLocator,
        ILogger<SharedMemoryLiveSessionSource> logger)
    {
        _sourceOptions = sourceOptions;
        _timeProvider = timeProvider;
        _snapshotBuilder = snapshotBuilder;
        _store = store;
        _parser = parser;
        _telemetryParser = telemetryParser;
        _liveDataPublisher = liveDataPublisher;
        _mapReader = mapReader;
        _mapDiscovery = mapDiscovery;
        _mapResolver = mapResolver;
        _payloadLocator = payloadLocator;

        var options = sourceOptions.CurrentValue.SharedMemory;
        _scoringLoop = new SharedMemoryPollingLoop<ScoringLoopValue>(
            "scoring",
            enabled: true,
            PollInterval(options.ScoringPollHz),
            ReadScoringAsync,
            logger);

        _telemetryLoop = new SharedMemoryPollingLoop<TelemetryLoopValue>(
            "telemetry",
            options.Telemetry.Enabled,
            PollInterval(options.Telemetry.PollHz),
            ReadTelemetryAsync,
            logger);
    }

    /// <inheritdoc />
    public async Task<LiveSessionSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var sequence = Interlocked.Increment(ref _updateSequence);
        var timestampUtc = _timeProvider.GetUtcNow();
        var scoring = _scoringLoop.Latest;
        if (scoring is null)
        {
            return new LiveSessionSnapshot
            {
                Source = "shared-memory",
                Status = "waiting for rF2 scoring map",
                TimestampUtc = timestampUtc,
                UpdateSequence = sequence,
                Session = new LiveSessionInfo
                {
                    TrackName = "No live scoring source",
                    Kind = SessionKind.Unknown,
                    Phase = SessionPhase.Unknown,
                    OverallFlag = "Unavailable",
                },
            };
        }

        var source = scoring.Source with
        {
            Source = "shared-memory",
            Status = "connected",
        };

        return _snapshotBuilder.Build(source, await GetAliasMapAsync(cancellationToken), timestampUtc, sequence);
    }

    private async Task<DriverAliasMap> GetAliasMapAsync(CancellationToken cancellationToken)
    {
        if (_store.IsEnabled)
        {
            return new DriverAliasMap(await _store.GetDriverAliasesAsync(cancellationToken));
        }

        return new DriverAliasMap(_sourceOptions.CurrentValue.DriverAliases);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _scoringLoop.Dispose();
        _telemetryLoop.Dispose();
    }

    private async Task<ScoringLoopValue?> ReadScoringAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = _sourceOptions.CurrentValue.SharedMemory;
        var discoveredMaps = options.AutoDiscover ? _mapDiscovery.DiscoverScoringMaps(options) : [];
        var resolution = _mapResolver.Resolve(options, discoveredMaps);
        if (resolution.IsAmbiguous)
        {
            return null;
        }

        if (!_mapReader.TryReadFirstAvailable(resolution.CandidateMapNames, _parser.PayloadSize, out var read, out _))
        {
            return null;
        }

        var location = _payloadLocator.Locate(read.Buffer, _parser.PayloadSize, _parser.ScorePayload);
        var payload = read.Buffer.AsSpan(location.Offset, location.PayloadSize);
        if (!_parser.IsStablePayload(payload))
        {
            return null;
        }

        var source = _parser.ParseSource(
            payload,
            "shared-memory",
            $"{resolution.Status}; connected to {read.MapName}");

        await _liveDataPublisher.PublishAsync(new ScoringContextFrame
        {
            Snapshot = _snapshotBuilder.Build(source, DriverAliasMap.Empty, _timeProvider.GetUtcNow(), 0),
        }, cancellationToken);

        return new ScoringLoopValue(source, read.MapName, location.Offset);
    }

    private async Task<TelemetryLoopValue?> ReadTelemetryAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = _sourceOptions.CurrentValue.SharedMemory;
        var explicitTelemetryMapName = DeriveTelemetryMapName(_scoringLoop.Latest?.MapName);
        var candidateMapNames = Rf2SharedMemoryMapReader.CandidateMapNames(
            Rf2SharedMemoryMapReader.TelemetryMapName,
            explicitTelemetryMapName,
            options.ProcessId);

        if (!_mapReader.TryReadFirstAvailable(candidateMapNames, _telemetryParser.PayloadSize, out var read, out _))
        {
            return null;
        }

        var location = _payloadLocator.Locate(read.Buffer, _telemetryParser.PayloadSize, _telemetryParser.ScorePayload);
        var payload = read.Buffer.AsSpan(location.Offset, location.PayloadSize);
        if (!_telemetryParser.IsStablePayload(payload))
        {
            return null;
        }

        var frame = _telemetryParser.ParsePositionFrame(payload, "telemetry");
        await _liveDataPublisher.PublishAsync(frame, cancellationToken);
        return new TelemetryLoopValue($"connected to {read.MapName}; vehicles={frame.Vehicles.Count}");
    }

    private static string? DeriveTelemetryMapName(string? scoringMapName)
    {
        if (string.IsNullOrWhiteSpace(scoringMapName))
        {
            return null;
        }

        var telemetryMapName = scoringMapName.Replace("Scoring", "Telemetry", StringComparison.OrdinalIgnoreCase);
        return string.Equals(telemetryMapName, scoringMapName, StringComparison.OrdinalIgnoreCase)
            ? null
            : telemetryMapName;
    }

    private static TimeSpan PollInterval(double hertz) => TimeSpan.FromSeconds(1.0 / Math.Clamp(hertz, 0.25, 200.0));

    private sealed record ScoringLoopValue(LeaderboardSourceSnapshot Source, string MapName, int DecodeOffset);

    private sealed record TelemetryLoopValue(string Status);
}