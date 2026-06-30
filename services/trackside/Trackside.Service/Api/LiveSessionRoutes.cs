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
    /// REST endpoint for the current track's generated driver-tracker geometry.
    /// </summary>
    public const string CurrentTrackGeometryPath = "/api/track-geometry/current";

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
    /// Admin endpoint for kiosk display defaults.
    /// </summary>
    public const string AdminKioskPath = "/api/admin/kiosk";

    /// <summary>
    /// Admin endpoint for driver tracker display settings.
    /// </summary>
    public const string AdminDriverTrackerPath = "/api/admin/driver-tracker";

    /// <summary>
    /// Admin endpoint for driver tracker generated-geometry track catalog.
    /// </summary>
    public const string AdminDriverTrackerTracksPath = "/api/admin/driver-tracker/tracks";

    /// <summary>
    /// Admin endpoint for starting generated-geometry recording passes.
    /// </summary>
    public const string AdminDriverTrackerRecordingsPath = "/api/admin/driver-tracker/recordings";

    /// <summary>
    /// Admin endpoint for frontend localization settings.
    /// </summary>
    public const string AdminLocalizationPath = "/api/admin/localization";

    /// <summary>
    /// Admin endpoint for persistence maintenance actions.
    /// </summary>
    public const string AdminPersistencePath = "/api/admin/persistence";

    /// <summary>
    /// Admin endpoint for live shared-memory diagnostics.
    /// </summary>
    public const string AdminSharedMemoryDebugPath = "/api/admin/shared-memory/debug";

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