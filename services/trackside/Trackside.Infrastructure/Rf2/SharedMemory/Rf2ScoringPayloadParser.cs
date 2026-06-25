using Trackside.Domain.LiveSession;

namespace Trackside.Infrastructure.Rf2.SharedMemory;

/// <summary>
/// Contract for future rFactor 2 scoring payload parsers.
/// </summary>
public interface IRf2ScoringPayloadParser
{
    /// <summary>
    /// Converts a scoring payload into the normalized Trackside live-session model.
    /// </summary>
    /// <param name="payload">Scoring payload bytes with any mapped-buffer wrapper already removed.</param>
    /// <param name="timestampUtc">Timestamp assigned to the normalized snapshot.</param>
    /// <param name="updateSequence">Host-local update sequence assigned to the snapshot.</param>
    /// <returns>A normalized live-session snapshot.</returns>
    LiveSessionSnapshot Parse(ReadOnlySpan<byte> payload, DateTimeOffset timestampUtc, long updateSequence);
}

/// <summary>
/// Phase 0B parser placeholder; the real struct parser starts in the shared-memory milestone.
/// </summary>
public sealed class Rf2ScoringPayloadParser : IRf2ScoringPayloadParser
{
    /// <inheritdoc />
    public LiveSessionSnapshot Parse(ReadOnlySpan<byte> payload, DateTimeOffset timestampUtc, long updateSequence)
    {
        throw new NotImplementedException("Shared-memory scoring parsing is intentionally not implemented in Phase 0B.");
    }
}