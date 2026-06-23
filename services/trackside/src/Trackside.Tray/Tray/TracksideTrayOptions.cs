namespace Trackside.Tray.Tray;

/// <summary>
/// Configuration for the Windows notification-area shell.
/// </summary>
public sealed class TracksideTrayOptions
{
    /// <summary>
    /// Configuration section name used by the tray companion.
    /// </summary>
    public const string SectionName = "TracksideTray";

    /// <summary>
    /// Base URL of the local Trackside service opened by relative tray menu routes.
    /// </summary>
    public string HostBaseUrl { get; init; } = "http://127.0.0.1:8877";

    /// <summary>
    /// Enables the tray icon when the executable is launched in an interactive Windows session.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Tooltip shown when hovering the notification-area icon.
    /// </summary>
    public string Tooltip { get; init; } = "Trackside";

    /// <summary>
    /// Shows a Windows notification balloon when the tray companion starts.
    /// Keep this off for normal venue operation to avoid noisy reboot/login behavior.
    /// </summary>
    public bool ShowStartupBalloon { get; init; }

    /// <summary>
    /// Balloon title shown when <see cref="ShowStartupBalloon" /> is enabled.
    /// </summary>
    public string BalloonTitle { get; init; } = "Trackside is running";

    /// <summary>
    /// Balloon body shown when <see cref="ShowStartupBalloon" /> is enabled.
    /// </summary>
    public string BalloonMessage { get; init; } = "Open the tray menu for Trackside actions.";

    /// <summary>
    /// Configurable context-menu entries. Items are rendered in this order.
    /// </summary>
    public List<TracksideTrayMenuItemOptions> MenuItems { get; init; } = [];
}

/// <summary>
/// One configurable tray context-menu item.
/// </summary>
public sealed class TracksideTrayMenuItemOptions
{
    /// <summary>
    /// Text displayed for clickable menu items.
    /// </summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>
    /// Action performed when the menu item is clicked.
    /// </summary>
    public TracksideTrayMenuAction Action { get; init; } = TracksideTrayMenuAction.OpenUrl;

    /// <summary>
    /// Absolute URL to open. When set, this takes precedence over <see cref="Route" />.
    /// </summary>
    public string? Url { get; init; }

    /// <summary>
    /// Application-relative route to open against the configured public base URL.
    /// </summary>
    public string? Route { get; init; }
}

/// <summary>
/// Supported tray menu actions.
/// </summary>
public enum TracksideTrayMenuAction
{
    /// <summary>Open an absolute URL or application route in the default browser.</summary>
    OpenUrl,

    /// <summary>Render a separator line between menu groups.</summary>
    Separator,

    /// <summary>Request graceful host shutdown.</summary>
    Exit,
}