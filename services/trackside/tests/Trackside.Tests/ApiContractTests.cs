using Trackside.Host.Api;

namespace Trackside.Tests;

/// <summary>
/// Verifies public routes shared by host, tray, and kiosk client code.
/// </summary>
public sealed class ApiContractTests
{
    /// <summary>
    /// Keeps the initial REST endpoint aligned with ADR-0001.
    /// </summary>
    [Fact]
    public void CurrentSessionPathMatchesArchitectureDecision()
    {
        Assert.Equal("/api/live-session/current", LiveSessionRoutes.CurrentSessionPath);
    }

    /// <summary>
    /// Keeps the SignalR hub path aligned with ADR-0001.
    /// </summary>
    [Fact]
    public void HubPathMatchesArchitectureDecision()
    {
        Assert.Equal("/hubs/live-session", LiveSessionRoutes.HubPath);
    }
}