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
    /// REST endpoint for public best-lap leaderboards.
    /// </summary>
    public const string BestLapsPath = "/api/leaderboards/best-laps";

    /// <summary>
    /// REST endpoint for the active monthly track period.
    /// </summary>
    public const string MonthlyTrackPath = "/api/leaderboards/monthly-track";

    /// <summary>
    /// REST endpoint for the most recently finished session result.
    /// </summary>
    public const string LastFinishedSessionPath = "/api/leaderboards/last-session";

    /// <summary>
    /// Admin endpoint for changing or resetting the active monthly track period.
    /// </summary>
    public const string AdminMonthlyTrackPath = "/api/admin/leaderboards/monthly-track";

    /// <summary>
    /// Admin endpoint for prepared rig/name/profile setup.
    /// </summary>
    public const string AdminSessionSetupPath = "/api/admin/session-setup";

    /// <summary>
    /// Admin endpoint for optional recurring-customer driver profiles.
    /// </summary>
    public const string AdminDriverProfilesPath = "/api/admin/driver-profiles";

    /// <summary>
    /// Admin endpoint for persisted sessions and participants.
    /// </summary>
    public const string AdminSessionsPath = "/api/admin/sessions";

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