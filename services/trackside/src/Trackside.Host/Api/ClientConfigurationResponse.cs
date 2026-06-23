namespace Trackside.Host.Api;

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
    /// Health endpoint path used by diagnostics pages and tray menu items.
    /// </summary>
    public string HealthPath { get; init; } = LiveSessionRoutes.HealthPath;

    /// <summary>
    /// Recommended delay before a browser attempts SignalR reconnect.
    /// </summary>
    public double RecommendedReconnectSeconds { get; init; } = 2.0;
}