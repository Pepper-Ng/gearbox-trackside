using Trackside.Domain.LiveSession;

namespace Trackside.Application.LiveSession;

/// <summary>
/// Provides normalized live-session snapshots without exposing where they came from.
/// </summary>
public interface ILiveSessionSource
{
    /// <summary>
    /// Reads the current session snapshot from the configured source.
    /// </summary>
    /// <param name="cancellationToken">Token used when the host is shutting down.</param>
    /// <returns>The latest normalized live-session snapshot.</returns>
    Task<LiveSessionSnapshot> GetCurrentAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Source modes supported by the Phase 0B host shape.
/// </summary>
public enum LiveSessionSourceMode
{
    /// <summary>Read normalized JSON fixtures from disk.</summary>
    Fixture,

    /// <summary>Future source that reads rFactor 2 shared memory maps.</summary>
    SharedMemory,

    /// <summary>Future source that replays recorded snapshots from disk.</summary>
    Recorded,
}