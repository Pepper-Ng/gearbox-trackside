using Trackside.Domain.LiveSession;

namespace Trackside.Host.Hubs;

/// <summary>
/// Strongly typed SignalR client contract for live-session updates.
/// </summary>
public interface ILiveSessionClient
{
    /// <summary>
    /// Receives a complete normalized live-session snapshot.
    /// </summary>
    /// <param name="snapshot">Snapshot that replaces the client's current view.</param>
    /// <returns>A task that completes when SignalR has dispatched the message.</returns>
    Task SessionUpdated(LiveSessionSnapshot snapshot);
}