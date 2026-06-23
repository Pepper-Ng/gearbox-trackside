using Trackside.Domain.LiveSession;

namespace Trackside.Application.LiveSession;

/// <summary>
/// Thread-safe cache of the most recent normalized live-session snapshot.
/// </summary>
public sealed class LiveSessionState
{
    private LiveSessionSnapshot? _current;

    /// <summary>
    /// Most recently published snapshot, or null before the first source read succeeds.
    /// </summary>
    public LiveSessionSnapshot? Current => Volatile.Read(ref _current);

    /// <summary>
    /// Replaces the cached current snapshot.
    /// </summary>
    /// <param name="snapshot">Snapshot that should become visible to API and hub callers.</param>
    public void Update(LiveSessionSnapshot snapshot) => Volatile.Write(ref _current, snapshot);
}