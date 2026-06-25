namespace Trackside.Domain.LiveSession;

/// <summary>
/// Sector timing values for one driver row.
/// </summary>
public sealed record SectorSnapshot
{
    /// <summary>
    /// One-based sector number.
    /// </summary>
    public int Number { get; init; }

    /// <summary>
    /// Driver's best time in this sector, in seconds.
    /// </summary>
    public double? BestSeconds { get; init; }

    /// <summary>
    /// Driver's most recently completed time in this sector, in seconds.
    /// </summary>
    public double? LastSeconds { get; init; }

    /// <summary>
    /// Driver's current in-progress time for this sector, in seconds, when known.
    /// </summary>
    public double? CurrentSeconds { get; init; }

    /// <summary>
    /// True when this sector currently represents the best known sector overall.
    /// </summary>
    public bool IsOverallBest { get; init; }
}