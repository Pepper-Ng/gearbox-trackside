using Trackside.Service.Api;

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

    /// <summary>
    /// Keeps the health endpoint aligned with deployment scripts and tray diagnostics.
    /// </summary>
    [Fact]
    public void HealthPathMatchesArchitectureDecision()
    {
        Assert.Equal("/api/health", LiveSessionRoutes.HealthPath);
    }

    /// <summary>
    /// Keeps the source configuration endpoint stable for the tray menu and configuration page.
    /// </summary>
    [Fact]
    public void SourceConfigurationPathIsStable()
    {
        Assert.Equal("/api/configuration/source", LiveSessionRoutes.SourceConfigurationPath);
    }

    /// <summary>
    /// Keeps the admin session endpoint stable for the admin dashboard.
    /// </summary>
    [Fact]
    public void AdminSessionPathIsStable()
    {
        Assert.Equal("/api/admin/session", LiveSessionRoutes.AdminSessionPath);
    }

    /// <summary>
    /// Keeps the admin status endpoint stable for the admin dashboard.
    /// </summary>
    [Fact]
    public void AdminStatusPathIsStable()
    {
        Assert.Equal("/api/admin/status", LiveSessionRoutes.AdminStatusPath);
    }
}