using Trackside.Domain.LiveSession;
using Trackside.Service.Tracking;

namespace Trackside.Service.Hubs;

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

    /// <summary>
    /// Receives generated track geometry for the current live-session track.
    /// </summary>
    /// <param name="geometry">Generated track geometry response.</param>
    /// <returns>A task that completes when SignalR has dispatched the message.</returns>
    Task TrackGeometryUpdated(TrackGeometryResponse geometry);
}