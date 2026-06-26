using System.ComponentModel.DataAnnotations;
using Trackside.Application.Configuration;

namespace Trackside.Service.Configuration;

/// <summary>
/// Root application options loaded from the <c>Trackside</c> configuration section.
/// </summary>
public sealed class TracksideOptions
{
    /// <summary>
    /// Configuration section name used for all Trackside-specific settings.
    /// </summary>
    public const string SectionName = "Trackside";

    /// <summary>
    /// HTTP binding and public URL settings for the local host.
    /// </summary>
    public TracksideHttpOptions Http { get; init; } = new();

    /// <summary>
    /// Data-source selection and source-specific configuration.
    /// </summary>
    public TracksideSourceOptions Source { get; init; } = new();

    /// <summary>
    /// Live-session publishing cadence for browser clients.
    /// </summary>
    public TracksideLiveSessionOptions LiveSession { get; init; } = new();

    /// <summary>
    /// Durable persistence settings for Phase 2 staff workflows and historical boards.
    /// </summary>
    public TracksidePersistenceOptions Persistence { get; init; } = new();

    /// <summary>
    /// Browser origins allowed to call the local API during frontend development.
    /// </summary>
    public TracksideCorsOptions Cors { get; init; } = new();

    /// <summary>
    /// Installed runtime layout surfaced through diagnostics and used by scripts.
    /// </summary>
    public TracksideDeploymentOptions Deployment { get; init; } = new();

    /// <summary>
    /// Update-check placeholders used by health and the future admin dashboard.
    /// </summary>
    public TracksideUpdateOptions Updates { get; init; } = new();
}

/// <summary>
/// HTTP binding options for the Trackside local web server.
/// </summary>
public sealed class TracksideHttpOptions
{
    /// <summary>
    /// Default loopback address used by development and venue-local packaged runs.
    /// </summary>
    public const string DefaultListenUrl = "http://127.0.0.1:8877";

    /// <summary>
    /// URL prefix Kestrel listens on. Keep this loopback-only unless LAN access is intentional.
    /// </summary>
    [Required]
    public string ListenUrl { get; init; } = DefaultListenUrl;

    /// <summary>
    /// Base URL opened from tray menu items and returned to browser clients.
    /// </summary>
    [Required]
    public string PublicBaseUrl { get; init; } = DefaultListenUrl;
}

/// <summary>
/// CORS settings for local frontend development.
/// </summary>
public sealed class TracksideCorsOptions
{
    /// <summary>
    /// Explicit browser origins allowed to call the local API and SignalR hub.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];
}

/// <summary>
/// Runtime installation layout used by packaged Trackside builds.
/// </summary>
public sealed class TracksideDeploymentOptions
{
    /// <summary>
    /// Default install mode used by repo-local development runs.
    /// </summary>
    public const string DefaultInstallMode = "Development";

    /// <summary>
    /// Logical install mode, for example Development, Service, BundleSmoke, or RigAgent.
    /// </summary>
    [Required]
    public string InstallMode { get; init; } = DefaultInstallMode;

    /// <summary>
    /// Root folder of the installed Trackside bundle, when installed.
    /// </summary>
    public string? InstallRoot { get; init; }

    /// <summary>
    /// External configuration root. Packaged installs use service and tray subfolders below this path.
    /// </summary>
    public string? ConfigPath { get; init; }

    /// <summary>
    /// Writable data directory for SQLite and durable runtime state.
    /// </summary>
    public string? DataPath { get; init; }

    /// <summary>
    /// Writable logs directory for service, tray, updater, and diagnostics output.
    /// </summary>
    public string? LogsPath { get; init; }

    /// <summary>
    /// Writable update staging directory for downloaded or candidate bundles.
    /// </summary>
    public string? UpdatesPath { get; init; }

    /// <summary>
    /// Manifest path for the currently installed bundle.
    /// </summary>
    public string? ManifestPath { get; init; }

    /// <summary>
    /// Versioned bundle identifier produced by the package script.
    /// </summary>
    public string? BundleVersion { get; init; }

    /// <summary>
    /// Windows Service name used by install/uninstall scripts.
    /// </summary>
    [Required]
    public string ServiceName { get; init; } = "Trackside";

    /// <summary>
    /// Current-user Run entry name used for tray auto-start.
    /// </summary>
    [Required]
    public string TrayAutostartName { get; init; } = "Trackside Tray";
}

/// <summary>
/// Placeholder update-check configuration for future dashboard-controlled updates.
/// </summary>
public sealed class TracksideUpdateOptions
{
    /// <summary>
    /// Default status before remote update checks exist.
    /// </summary>
    public const string DefaultStatus = "NotConfigured";

    /// <summary>
    /// Human-readable update status surfaced in health responses.
    /// </summary>
    [Required]
    public string Status { get; init; } = DefaultStatus;

    /// <summary>
    /// Update channel name used when remote manifests are introduced.
    /// </summary>
    [Required]
    public string Channel { get; init; } = "local";

    /// <summary>
    /// Remote update manifest URL. Empty until update checks are configured.
    /// </summary>
    public string? ManifestUrl { get; init; }

    /// <summary>
    /// Local candidate manifest path used by installer/updater smoke tests.
    /// </summary>
    public string? CandidateManifestPath { get; init; }

    /// <summary>
    /// Minimum app version that can safely consume current update manifests.
    /// </summary>
    [Required]
    public string MinimumCompatibleVersion { get; init; } = "0.1.0";
}