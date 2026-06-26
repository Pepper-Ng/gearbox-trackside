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
    /// Keeps the public best-lap endpoint stable for kiosk screens.
    /// </summary>
    [Fact]
    public void BestLapsPathIsStable()
    {
        Assert.Equal("/api/leaderboards/best-laps", LiveSessionRoutes.BestLapsPath);
    }

    /// <summary>
    /// Keeps the public monthly-track endpoint stable for kiosk screens.
    /// </summary>
    [Fact]
    public void MonthlyTrackPathIsStable()
    {
        Assert.Equal("/api/leaderboards/monthly-track", LiveSessionRoutes.MonthlyTrackPath);
    }

    /// <summary>
    /// Keeps the admin monthly-track endpoint stable for the admin dashboard.
    /// </summary>
    [Fact]
    public void AdminMonthlyTrackPathIsStable()
    {
        Assert.Equal("/api/admin/leaderboards/monthly-track", LiveSessionRoutes.AdminMonthlyTrackPath);
    }

    /// <summary>
    /// Keeps the public last-session endpoint stable for kiosk screens.
    /// </summary>
    [Fact]
    public void LastFinishedSessionPathIsStable()
    {
        Assert.Equal("/api/leaderboards/last-session", LiveSessionRoutes.LastFinishedSessionPath);
    }

    /// <summary>
    /// Keeps prepared session setup endpoint stable for the admin dashboard.
    /// </summary>
    [Fact]
    public void AdminSessionSetupPathIsStable()
    {
        Assert.Equal("/api/admin/session-setup", LiveSessionRoutes.AdminSessionSetupPath);
    }

    /// <summary>
    /// Keeps driver profile endpoint stable for the admin dashboard.
    /// </summary>
    [Fact]
    public void AdminDriverProfilesPathIsStable()
    {
        Assert.Equal("/api/admin/driver-profiles", LiveSessionRoutes.AdminDriverProfilesPath);
    }

    /// <summary>
    /// Keeps persisted session browser endpoint stable for the admin dashboard.
    /// </summary>
    [Fact]
    public void AdminSessionsPathIsStable()
    {
        Assert.Equal("/api/admin/sessions", LiveSessionRoutes.AdminSessionsPath);
    }

    /// <summary>
    /// Keeps kiosk settings endpoint stable for the admin dashboard.
    /// </summary>
    [Fact]
    public void AdminKioskPathIsStable()
    {
        Assert.Equal("/api/admin/kiosk", LiveSessionRoutes.AdminKioskPath);
    }

    /// <summary>
    /// Keeps persistence maintenance endpoint stable for the admin dashboard.
    /// </summary>
    [Fact]
    public void AdminPersistencePathIsStable()
    {
        Assert.Equal("/api/admin/persistence", LiveSessionRoutes.AdminPersistencePath);
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