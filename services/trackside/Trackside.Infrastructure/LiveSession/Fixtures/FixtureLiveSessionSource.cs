using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Application.Persistence;
using Trackside.Application.Serialization;
using Trackside.Domain.LiveSession;

namespace Trackside.Infrastructure.LiveSession.Fixtures;

/// <summary>
/// Loads a normalized live-session snapshot from a JSON fixture file.
/// </summary>
public sealed class FixtureLiveSessionSource : ILiveSessionSource
{
    private readonly string _fixturePath;
    private readonly string _contentRootPath;
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<TracksideSourceOptions> _sourceOptions;
    private readonly ITracksideStore _store;
    private readonly ILeaderboardSnapshotBuilder _snapshotBuilder;
    private readonly ILogger<FixtureLiveSessionSource> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private LoadedFixture? _cachedFixture;
    private string? _cachedPath;
    private DateTime _cachedLastWriteUtc;
    private long _updateSequence;

    /// <summary>
    /// Creates a fixture source using host-provided path configuration.
    /// </summary>
    /// <param name="fixturePath">Configured fixture file path.</param>
    /// <param name="contentRootPath">Host content root used to resolve relative fixture paths.</param>
    /// <param name="timeProvider">Clock used to stamp refreshed snapshots.</param>
    /// <param name="sourceOptions">Source options containing current alias mappings.</param>
    /// <param name="store">Durable Phase 2 store used for staff aliases when enabled.</param>
    /// <param name="snapshotBuilder">Builder used to normalize raw scoring fixtures.</param>
    /// <param name="logger">Logger for reload and fixture parse events.</param>
    public FixtureLiveSessionSource(
        string fixturePath,
        string contentRootPath,
        TimeProvider timeProvider,
        IOptionsMonitor<TracksideSourceOptions> sourceOptions,
        ITracksideStore store,
        ILeaderboardSnapshotBuilder snapshotBuilder,
        ILogger<FixtureLiveSessionSource> logger)
    {
        _fixturePath = fixturePath;
        _contentRootPath = contentRootPath;
        _timeProvider = timeProvider;
        _sourceOptions = sourceOptions;
        _store = store;
        _snapshotBuilder = snapshotBuilder;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LiveSessionSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var fixturePath = ResolveFixturePath(_fixturePath);
        var fixture = await LoadFixtureAsync(fixturePath, cancellationToken);
        var sequence = Interlocked.Increment(ref _updateSequence);
        var timestampUtc = _timeProvider.GetUtcNow();

        if (fixture.LeaderboardSource is not null)
        {
            var source = fixture.LeaderboardSource with
            {
                Source = "fixture",
                Status = $"fixture scoring replay: {Path.GetFileName(fixturePath)}",
            };

            return _snapshotBuilder.Build(
                source,
                await GetAliasMapAsync(cancellationToken),
                timestampUtc,
                sequence);
        }

        var snapshot = fixture.NormalizedSnapshot ?? throw new InvalidDataException($"Fixture did not contain a supported payload: {fixturePath}");

        return snapshot with
        {
            Source = "fixture",
            Status = $"fixture replay: {Path.GetFileName(fixturePath)}",
            TimestampUtc = timestampUtc,
            UpdateSequence = sequence,
        };
    }

    private async Task<DriverAliasMap> GetAliasMapAsync(CancellationToken cancellationToken)
    {
        if (_store.IsEnabled)
        {
            return new DriverAliasMap(await _store.GetDriverAliasesAsync(cancellationToken));
        }

        return new DriverAliasMap(_sourceOptions.CurrentValue.DriverAliases);
    }

    private async Task<LoadedFixture> LoadFixtureAsync(string fixturePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException("The configured live-session fixture file does not exist.", fixturePath);
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(fixturePath);
        if (_cachedFixture is not null && fixturePath == _cachedPath && lastWriteUtc == _cachedLastWriteUtc)
        {
            return _cachedFixture;
        }

        await _reloadLock.WaitAsync(cancellationToken);
        try
        {
            lastWriteUtc = File.GetLastWriteTimeUtc(fixturePath);
            if (_cachedFixture is not null && fixturePath == _cachedPath && lastWriteUtc == _cachedLastWriteUtc)
            {
                return _cachedFixture;
            }

            await using var stream = File.OpenRead(fixturePath);
            var fixture = await DeserializeFixtureAsync(stream, fixturePath, cancellationToken);

            _cachedFixture = fixture;
            _cachedPath = fixturePath;
            _cachedLastWriteUtc = lastWriteUtc;
            _logger.LogInformation("Loaded live-session fixture {FixturePath}", fixturePath);

            return fixture;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private static async Task<LoadedFixture> DeserializeFixtureAsync(
        Stream stream,
        string fixturePath,
        CancellationToken cancellationToken)
    {
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.TryGetProperty("format", out var format)
            && string.Equals(format.GetString(), "rf2-scoring-leaderboard-v1", StringComparison.OrdinalIgnoreCase))
        {
            var source = document.RootElement.Deserialize<LeaderboardSourceSnapshot>(TracksideJson.SerializerOptions)
                ?? throw new InvalidDataException($"Fixture did not contain a {nameof(LeaderboardSourceSnapshot)} payload: {fixturePath}");

            return new LoadedFixture(null, source);
        }

        var snapshot = document.RootElement.Deserialize<LiveSessionSnapshot>(TracksideJson.SerializerOptions)
            ?? throw new InvalidDataException($"Fixture did not contain a {nameof(LiveSessionSnapshot)} payload: {fixturePath}");

        return new LoadedFixture(snapshot, null);
    }

    private string ResolveFixturePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(_contentRootPath, configuredPath));
    }

    private sealed record LoadedFixture(LiveSessionSnapshot? NormalizedSnapshot, LeaderboardSourceSnapshot? LeaderboardSource);
}