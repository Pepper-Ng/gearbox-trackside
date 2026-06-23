using System.Text.Json;
using Microsoft.Extensions.Logging;
using Trackside.Application.LiveSession;
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
    private readonly ILogger<FixtureLiveSessionSource> _logger;
    private readonly SemaphoreSlim _reloadLock = new(1, 1);
    private LiveSessionSnapshot? _cachedSnapshot;
    private string? _cachedPath;
    private DateTime _cachedLastWriteUtc;
    private long _updateSequence;

    /// <summary>
    /// Creates a fixture source using host-provided path configuration.
    /// </summary>
    /// <param name="fixturePath">Configured fixture file path.</param>
    /// <param name="contentRootPath">Host content root used to resolve relative fixture paths.</param>
    /// <param name="timeProvider">Clock used to stamp refreshed snapshots.</param>
    /// <param name="logger">Logger for reload and fixture parse events.</param>
    public FixtureLiveSessionSource(
        string fixturePath,
        string contentRootPath,
        TimeProvider timeProvider,
        ILogger<FixtureLiveSessionSource> logger)
    {
        _fixturePath = fixturePath;
        _contentRootPath = contentRootPath;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LiveSessionSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var fixturePath = ResolveFixturePath(_fixturePath);
        var snapshot = await LoadSnapshotAsync(fixturePath, cancellationToken);
        var sequence = Interlocked.Increment(ref _updateSequence);

        return snapshot with
        {
            Source = "fixture",
            Status = $"fixture replay: {Path.GetFileName(fixturePath)}",
            TimestampUtc = _timeProvider.GetUtcNow(),
            UpdateSequence = sequence,
        };
    }

    private async Task<LiveSessionSnapshot> LoadSnapshotAsync(string fixturePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(fixturePath))
        {
            throw new FileNotFoundException("The configured live-session fixture file does not exist.", fixturePath);
        }

        var lastWriteUtc = File.GetLastWriteTimeUtc(fixturePath);
        if (_cachedSnapshot is not null && fixturePath == _cachedPath && lastWriteUtc == _cachedLastWriteUtc)
        {
            return _cachedSnapshot;
        }

        await _reloadLock.WaitAsync(cancellationToken);
        try
        {
            lastWriteUtc = File.GetLastWriteTimeUtc(fixturePath);
            if (_cachedSnapshot is not null && fixturePath == _cachedPath && lastWriteUtc == _cachedLastWriteUtc)
            {
                return _cachedSnapshot;
            }

            await using var stream = File.OpenRead(fixturePath);
            var snapshot = await JsonSerializer.DeserializeAsync<LiveSessionSnapshot>(
                stream,
                TracksideJson.SerializerOptions,
                cancellationToken);

            if (snapshot is null)
            {
                throw new InvalidDataException($"Fixture did not contain a {nameof(LiveSessionSnapshot)} payload: {fixturePath}");
            }

            _cachedSnapshot = snapshot;
            _cachedPath = fixturePath;
            _cachedLastWriteUtc = lastWriteUtc;
            _logger.LogInformation("Loaded live-session fixture {FixturePath}", fixturePath);

            return snapshot;
        }
        finally
        {
            _reloadLock.Release();
        }
    }

    private string ResolveFixturePath(string configuredPath)
    {
        if (Path.IsPathRooted(configuredPath))
        {
            return configuredPath;
        }

        return Path.GetFullPath(Path.Combine(_contentRootPath, configuredPath));
    }
}