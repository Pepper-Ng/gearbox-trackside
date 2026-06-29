using Trackside.Service.Configuration;

namespace Trackside.Service.Api;

/// <summary>
/// Browser-facing endpoint map for the kiosk and admin frontend.
/// </summary>
public sealed record ClientConfigurationResponse
{
    /// <summary>
    /// REST path used for initial live-session load and reconnect recovery.
    /// </summary>
    public string CurrentSessionPath { get; init; } = LiveSessionRoutes.CurrentSessionPath;

    /// <summary>
    /// SignalR hub path used for live-session pushes.
    /// </summary>
    public string LiveSessionHubPath { get; init; } = LiveSessionRoutes.HubPath;

    /// <summary>
    /// REST path used by the driver tracker to fetch current track geometry.
    /// </summary>
    public string TrackGeometryPath { get; init; } = LiveSessionRoutes.CurrentTrackGeometryPath;

    /// <summary>
    /// Health endpoint path used by diagnostics pages and tray menu items.
    /// </summary>
    public string HealthPath { get; init; } = LiveSessionRoutes.HealthPath;

    /// <summary>
    /// Recommended delay before a browser attempts SignalR reconnect.
    /// </summary>
    public double RecommendedReconnectSeconds { get; init; } = 2.0;

    /// <summary>
    /// Default display mode a kiosk screen should open with.
    /// </summary>
    public KioskDisplayMode DefaultDisplayMode { get; init; } = KioskDisplayMode.Monthly;

    /// <summary>
    /// Browser-side driver tracker refresh/redraw rate in Hertz. Source freshness is determined separately.
    /// </summary>
    public double DriverTrackerClientRefreshHz { get; init; } = TracksideDriverTrackerOptions.DefaultClientRefreshHz;

    /// <summary>
    /// Default frontend language for kiosk and admin screens.
    /// </summary>
    public string DefaultLanguage { get; init; } = "en";
}