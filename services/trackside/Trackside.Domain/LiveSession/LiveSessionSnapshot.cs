namespace Trackside.Domain.LiveSession;

/// <summary>
/// Browser-facing snapshot of the current live session.
/// </summary>
public sealed record LiveSessionSnapshot
{
    /// <summary>
    /// Logical source that produced the snapshot, for example fixture or shared-memory.
    /// </summary>
    public string Source { get; init; } = "unknown";

    /// <summary>
    /// Human-readable source status intended for diagnostics.
    /// </summary>
    public string Status { get; init; } = "not initialized";

    /// <summary>
    /// UTC timestamp for when the host published or refreshed this snapshot.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>
    /// Monotonic host-local update sequence for reconnect and diagnostics.
    /// </summary>
    public long UpdateSequence { get; init; }

    /// <summary>
    /// Session-level metadata.
    /// </summary>
    public LiveSessionInfo Session { get; init; } = new();

    /// <summary>
    /// Current normalized driver rows.
    /// </summary>
    public List<DriverSnapshot> Drivers { get; init; } = [];
}