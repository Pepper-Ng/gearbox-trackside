namespace Trackside.Domain.LiveSession;

/// <summary>
/// Normalized session-level information independent of the underlying source.
/// </summary>
public sealed record LiveSessionInfo
{
    /// <summary>
    /// Human-readable track name.
    /// </summary>
    public string TrackName { get; init; } = "Unknown track";

    /// <summary>
    /// Session kind used for sorting and display decisions.
    /// </summary>
    public SessionKind Kind { get; init; } = SessionKind.Unknown;

    /// <summary>
    /// Current session phase.
    /// </summary>
    public SessionPhase Phase { get; init; } = SessionPhase.Unknown;

    /// <summary>
    /// Current elapsed session time in seconds.
    /// </summary>
    public double? CurrentSessionSeconds { get; init; }

    /// <summary>
    /// Scheduled session duration in seconds when known.
    /// </summary>
    public double? ScheduledDurationSeconds { get; init; }

    /// <summary>
    /// Ambient air temperature in Celsius.
    /// </summary>
    public double? AirTemperatureCelsius { get; init; }

    /// <summary>
    /// Track surface temperature in Celsius.
    /// </summary>
    public double? TrackTemperatureCelsius { get; init; }

    /// <summary>
    /// Concise flag state suitable for kiosk display.
    /// </summary>
    public string OverallFlag { get; init; } = "Unknown";
}

/// <summary>
/// High-level rFactor 2 session category.
/// </summary>
public enum SessionKind
{
    /// <summary>No recognized session kind is available.</summary>
    Unknown,

    /// <summary>Practice or test session.</summary>
    Practice,

    /// <summary>Qualifying session.</summary>
    Qualifying,

    /// <summary>Race session.</summary>
    Race,
}

/// <summary>
/// Coarse session phase used by the kiosk and future automation.
/// </summary>
public enum SessionPhase
{
    /// <summary>No recognized phase is available.</summary>
    Unknown,

    /// <summary>Cars are in garage or the session is not live.</summary>
    Garage,

    /// <summary>The session is active and timing should be live.</summary>
    GreenFlag,

    /// <summary>The session has finished.</summary>
    SessionOver,
}