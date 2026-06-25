using Trackside.Application.LiveSession;
using Trackside.Domain.LiveSession;
using Trackside.Service.Hubs;

namespace Trackside.Tests;

/// <summary>
/// Covers current-snapshot recovery used by reconnecting kiosk clients.
/// </summary>
public sealed class LiveSessionRecoveryTests
{
    /// <summary>
    /// The SignalR hub exposes the cached current snapshot for recovery after reconnect.
    /// </summary>
    [Fact]
    public async Task HubReturnsCurrentSnapshotFromState()
    {
        var state = new LiveSessionState();
        var snapshot = new LiveSessionSnapshot
        {
            Source = "test",
            Status = "ready",
            UpdateSequence = 42,
            Session = new LiveSessionInfo { TrackName = "Recovery Track" },
        };
        state.Update(snapshot);
        var hub = new LiveSessionHub(state);

        var current = await hub.GetCurrentSession();

        Assert.Same(snapshot, current);
    }
}