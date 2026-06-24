namespace Trackside.Domain.LiveSession;

/// <summary>
/// Normalized driver row shown to kiosk and admin clients.
/// </summary>
public sealed record DriverSnapshot
{
    /// <summary>
    /// One-based leaderboard rank after Trackside sorting rules have been applied.
    /// </summary>
    public int LeaderboardRank { get; init; }

    /// <summary>
    /// Stable source-provided vehicle or scoring identifier for this row.
    /// </summary>
    public string DriverId { get; init; } = string.Empty;

    /// <summary>
    /// Physical rig or underlying rFactor 2 name, such as Setup1.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Staff-facing display name; initially this may match <see cref="RigName" />.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Vehicle name reported by the source or fixture.
    /// </summary>
    public string VehicleName { get; init; } = string.Empty;

    /// <summary>
    /// Current scored position when the source provides one.
    /// </summary>
    public int? Position { get; init; }

    /// <summary>
    /// True when this row owns the best known lap in the current snapshot.
    /// </summary>
    public bool IsOverallBestLap { get; init; }

    /// <summary>
    /// Completed lap count for this driver.
    /// </summary>
    public int CompletedLaps { get; init; }

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
    /// Gap to the current leader in seconds when available.
    /// </summary>
    public double? GapToLeaderSeconds { get; init; }

    /// <summary>
    /// Gap to the next car ahead in seconds when available.
    /// </summary>
    public double? GapToNextSeconds { get; init; }

    /// <summary>
    /// Laps behind the current leader when available.
    /// </summary>
    public int? LapsBehindLeader { get; init; }

    /// <summary>
    /// Current zero-based rFactor 2 sector index when available.
    /// </summary>
    public int? CurrentSector { get; init; }

    /// <summary>
    /// Approximate lap progress percentage from scoring data when available.
    /// </summary>
    public double? TrackPositionPercent { get; init; }

    /// <summary>
    /// Sector timing details for this driver.
    /// </summary>
    public List<SectorSnapshot> Sectors { get; init; } = [];
}