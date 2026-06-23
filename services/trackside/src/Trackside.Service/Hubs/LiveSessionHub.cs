using Microsoft.AspNetCore.SignalR;
using Trackside.Application.LiveSession;
using Trackside.Domain.LiveSession;

namespace Trackside.Service.Hubs;

/// <summary>
/// SignalR hub used by kiosk and admin pages to receive live-session pushes.
/// </summary>
public sealed class LiveSessionHub : Hub<ILiveSessionClient>
{
    private readonly LiveSessionState _state;

    /// <summary>
    /// Creates a live-session hub backed by the shared current-snapshot cache.
    /// </summary>
    /// <param name="state">Current live-session state cache.</param>
    public LiveSessionHub(LiveSessionState state)
    {
        _state = state;
    }

    /// <summary>
    /// Returns the current snapshot for clients that prefer hub-based initial load.
    /// </summary>
    /// <returns>The latest snapshot, or null if no source read has completed yet.</returns>
    public Task<LiveSessionSnapshot?> GetCurrentSession() => Task.FromResult(_state.Current);
}