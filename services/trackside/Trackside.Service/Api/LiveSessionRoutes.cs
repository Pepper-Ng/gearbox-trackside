namespace Trackside.Service.Api;

/// <summary>
/// Centralizes public API and hub routes so backend, tray, and frontend stay aligned.
/// </summary>
public static class LiveSessionRoutes
{
    /// <summary>
    /// REST endpoint for the latest normalized session snapshot.
    /// </summary>
    public const string CurrentSessionPath = "/api/live-session/current";

    /// <summary>
    /// REST endpoint for host health and source status.
    /// </summary>
    public const string HealthPath = "/api/health";

    /// <summary>
    /// REST endpoint for browser client configuration.
    /// </summary>
    public const string ClientConfigurationPath = "/api/configuration/client";

    /// <summary>
    /// SignalR hub path for live-session snapshot pushes.
    /// </summary>
    public const string HubPath = "/hubs/live-session";
}