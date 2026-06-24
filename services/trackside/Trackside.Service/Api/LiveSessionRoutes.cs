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
    /// REST endpoint for editable source and shared-memory discovery configuration.
    /// </summary>
    public const string SourceConfigurationPath = "/api/configuration/source";

    /// <summary>
    /// REST endpoint for initial admin bootstrap status and creation.
    /// </summary>
    public const string AdminBootstrapPath = "/api/admin/bootstrap";

    /// <summary>
    /// REST endpoint for admin login/session state.
    /// </summary>
    public const string AdminSessionPath = "/api/admin/session";

    /// <summary>
    /// REST endpoint for admin user management.
    /// </summary>
    public const string AdminUsersPath = "/api/admin/users";

    /// <summary>
    /// REST endpoint for admin-only service status.
    /// </summary>
    public const string AdminStatusPath = "/api/admin/status";

    /// <summary>
    /// SignalR hub path for live-session snapshot pushes.
    /// </summary>
    public const string HubPath = "/hubs/live-session";
}