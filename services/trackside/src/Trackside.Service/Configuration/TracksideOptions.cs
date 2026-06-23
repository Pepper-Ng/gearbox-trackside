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
    /// Browser origins allowed to call the local API during frontend development.
    /// </summary>
    public TracksideCorsOptions Cors { get; init; } = new();

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