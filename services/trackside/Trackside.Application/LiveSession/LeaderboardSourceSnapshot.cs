using Trackside.Domain.LiveSession;

namespace Trackside.Application.LiveSession;

/// <summary>
/// Source-facing live leaderboard snapshot that mirrors the scoring channels needed from rFactor 2.
/// </summary>
public sealed record LeaderboardSourceSnapshot
{
    /// <summary>
    /// Logical source that produced the raw leaderboard channels.
    /// </summary>
    public string Source { get; init; } = "unknown";

    /// <summary>
    /// Human-readable source status intended for diagnostics.
    /// </summary>
    public string Status { get; init; } = "not initialized";

    /// <summary>
    /// Raw session-level scoring channels used by the leaderboard.
    /// </summary>
    public LeaderboardSessionSource Session { get; init; } = new();

    /// <summary>
    /// Raw vehicle scoring rows used by the leaderboard.
    /// </summary>
    public List<LeaderboardDriverSource> Drivers { get; init; } = [];
}

/// <summary>
/// Source-facing session channels used to populate the live board header.
/// </summary>
public sealed record LeaderboardSessionSource
{
    /// <summary>
    /// Human-readable track name.
    /// </summary>
    public string TrackName { get; init; } = "Unknown track";

    /// <summary>
    /// Session category used for leaderboard ordering.
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
    /// Number of visible scoring rows when known.
    /// </summary>
    public int? VehicleCount { get; init; }

    /// <summary>
    /// Full lap distance in meters when known.
    /// </summary>
    public double? LapDistanceMeters { get; init; }

    /// <summary>
    /// Ambient air temperature in Celsius.
    /// </summary>
    public double? AirTemperatureCelsius { get; init; }

    /// <summary>
    /// Track surface temperature in Celsius.
    /// </summary>
    public double? TrackTemperatureCelsius { get; init; }

    /// <summary>
    /// Rain intensity from scoring, normalized between 0 and 1 when available.
    /// </summary>
    public double? RainIntensity { get; init; }

    /// <summary>
    /// Cloud intensity from scoring, normalized between 0 and 1 when available.
    /// </summary>
    public double? CloudIntensity { get; init; }

    /// <summary>
    /// Average track wetness from scoring, normalized between 0 and 1 when available.
    /// </summary>
    public double? TrackWetness { get; init; }

    /// <summary>
    /// Concise overall flag text.
    /// </summary>
    public string OverallFlag { get; init; } = "Unknown";
}

/// <summary>
/// Source-facing driver scoring channels used by the leaderboard.
/// </summary>
public sealed record LeaderboardDriverSource
{
    /// <summary>
    /// Stable scoring vehicle identifier.
    /// </summary>
    public string DriverId { get; init; } = string.Empty;

    /// <summary>
    /// Underlying rFactor 2 driver or rig name, such as Setup1.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Vehicle name reported by the source.
    /// </summary>
    public string VehicleName { get; init; } = string.Empty;

    /// <summary>
    /// Current race/scoring position when available.
    /// </summary>
    public int? RacePosition { get; init; }

    /// <summary>
    /// Completed lap count.
    /// </summary>
    public int CompletedLaps { get; init; }

    /// <summary>
    /// rFactor 2 valid-lap flag for the currently tracked lap: 0 does not count, 1 counts lap only, 2 counts lap and time.
    /// </summary>
    public int? ValidLapFlag { get; init; }

    /// <summary>
    /// Best completed lap time in seconds.
    /// </summary>
    public double? BestLapSeconds { get; init; }

    /// <summary>
    /// Most recently completed lap time in seconds.
    /// </summary>
    public double? LastLapSeconds { get; init; }

    /// <summary>
    /// Current in-progress lap time in seconds.
    /// </summary>
    public double? CurrentLapSeconds { get; init; }

    /// <summary>
    /// Current zero-based rFactor 2 sector index.
    /// </summary>
    public int? CurrentSector { get; init; }

    /// <summary>
    /// Best sector 1 time in seconds.
    /// </summary>
    public double? BestSector1Seconds { get; init; }

    /// <summary>
    /// Best sector 2 cumulative time in seconds, matching rFactor 2 scoring semantics.
    /// </summary>
    public double? BestSector2CumulativeSeconds { get; init; }

    /// <summary>
    /// Sector 1 time from the driver's best lap in seconds.
    /// </summary>
    public double? BestLapSector1Seconds { get; init; }

    /// <summary>
    /// Cumulative sector 2 time from the driver's best lap in seconds.
    /// </summary>
    public double? BestLapSector2CumulativeSeconds { get; init; }

    /// <summary>
    /// Last completed sector 1 time in seconds.
    /// </summary>
    public double? LastSector1Seconds { get; init; }

    /// <summary>
    /// Last completed sector 2 cumulative time in seconds.
    /// </summary>
    public double? LastSector2CumulativeSeconds { get; init; }

    /// <summary>
    /// Current lap sector 1 time in seconds.
    /// </summary>
    public double? CurrentSector1Seconds { get; init; }

    /// <summary>
    /// Current lap sector 2 cumulative time in seconds.
    /// </summary>
    public double? CurrentSector2CumulativeSeconds { get; init; }

    /// <summary>
    /// Gap to the next car ahead in seconds when available.
    /// </summary>
    public double? GapToNextSeconds { get; init; }

    /// <summary>
    /// Gap to the leader in seconds when available.
    /// </summary>
    public double? GapToLeaderSeconds { get; init; }

    /// <summary>
    /// Laps behind the leader when available.
    /// </summary>
    public int? LapsBehindLeader { get; init; }

    /// <summary>
    /// Lap distance in meters from the scoring source.
    /// </summary>
    public double? LapDistanceMeters { get; init; }

    /// <summary>
    /// Track position as a percentage of the current lap.
    /// </summary>
    public double? TrackPositionPercent { get; init; }

    /// <summary>
    /// Exact world X coordinate from the scoring payload when available.
    /// </summary>
    public double? PosX { get; init; }

    /// <summary>
    /// Exact world Y coordinate from the scoring payload when available.
    /// </summary>
    public double? PosY { get; init; }

    /// <summary>
    /// Exact world Z coordinate from the scoring payload when available.
    /// </summary>
    public double? PosZ { get; init; }
}