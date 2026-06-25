using Trackside.Tray.Tray;

namespace Trackside.Tests;

/// <summary>
/// Covers tray icon red/blue/green status classification.
/// </summary>
public sealed class TrayStatusClassifierTests
{
    /// <summary>
    /// Missing service data is shown as no memory-map connection.
    /// </summary>
    [Fact]
    public void NullObservationIsDisconnected()
    {
        Assert.Equal(TrayConnectionStatus.Disconnected, TrayStatusClassifier.Classify(null));
    }

    /// <summary>
    /// Fixture snapshots are not real memory-map connections.
    /// </summary>
    [Fact]
    public void FixtureSnapshotIsDisconnected()
    {
        var observation = new TrayStatusObservation("fixture", "fixture scoring replay", new TraySessionObservation("Practice", "GreenFlag"));

        Assert.Equal(TrayConnectionStatus.Disconnected, TrayStatusClassifier.Classify(observation));
    }

    /// <summary>
    /// Connected shared-memory snapshots without an active session are blue.
    /// </summary>
    [Fact]
    public void ConnectedMemoryMapWithoutActiveSessionIsBlueState()
    {
        var observation = new TrayStatusObservation("shared-memory", "connected to $rFactor2SMMP_Scoring$123", new TraySessionObservation("Unknown", "Garage"));

        Assert.Equal(TrayConnectionStatus.MemoryMapConnected, TrayStatusClassifier.Classify(observation));
    }

    /// <summary>
    /// Connected shared-memory snapshots with an active session are green.
    /// </summary>
    [Fact]
    public void ConnectedMemoryMapWithActiveSessionIsGreenState()
    {
        var observation = new TrayStatusObservation("shared-memory", "connected to $rFactor2SMMP_Scoring$123", new TraySessionObservation("Race", "GreenFlag"));

        Assert.Equal(TrayConnectionStatus.ActiveSession, TrayStatusClassifier.Classify(observation));
    }
}