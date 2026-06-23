using Trackside.Application.LiveSession;
using Trackside.Domain.LiveSession;

namespace Trackside.Infrastructure.LiveSession;

/// <summary>
/// Placeholder source used for future modes that are intentionally not implemented in Phase 0B.
/// </summary>
public sealed class UnsupportedLiveSessionSource : ILiveSessionSource
{
    private readonly LiveSessionSourceMode _mode;
    private readonly TimeProvider _timeProvider;
    private long _updateSequence;

    /// <summary>
    /// Creates an explicit placeholder for a configured but unavailable source mode.
    /// </summary>
    /// <param name="mode">Source mode requested by configuration.</param>
    /// <param name="timeProvider">Clock used to timestamp placeholder snapshots.</param>
    public UnsupportedLiveSessionSource(LiveSessionSourceMode mode, TimeProvider timeProvider)
    {
        _mode = mode;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<LiveSessionSnapshot> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var sequence = Interlocked.Increment(ref _updateSequence);
        var snapshot = new LiveSessionSnapshot
        {
            Source = _mode.ToString(),
            Status = $"{_mode} source is scaffolded but not implemented in Phase 0B.",
            TimestampUtc = _timeProvider.GetUtcNow(),
            UpdateSequence = sequence,
            Session = new LiveSessionInfo
            {
                TrackName = "No live source",
                Kind = SessionKind.Unknown,
                Phase = SessionPhase.Unknown,
                OverallFlag = "Unavailable",
            },
        };

        return Task.FromResult(snapshot);
    }
}