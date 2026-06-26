using System.ComponentModel.DataAnnotations;

namespace Trackside.Application.Configuration;

/// <summary>
/// Durable storage options for staff workflows and historical leaderboard data.
/// </summary>
public sealed class TracksidePersistenceOptions
{
    /// <summary>
    /// True when Trackside should use SQLite-backed Phase 2 persistence.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Optional absolute or relative SQLite database path. When omitted, Trackside uses the deployment data directory.
    /// </summary>
    public string? DatabasePath { get; init; }

    /// <summary>
    /// SQLite file name used inside the resolved data directory when <see cref="DatabasePath" /> is omitted.
    /// </summary>
    [Required]
    public string DatabaseFileName { get; init; } = "trackside.db";

    /// <summary>
    /// Default inclusion flag applied to newly observed live sessions until staff controls override it.
    /// </summary>
    public bool CountSessionsByDefault { get; init; } = true;

    /// <summary>
    /// Retention policy targets for persisted Trackside data. Cleanup enforcement is introduced only after derived records are safe.
    /// </summary>
    public TracksideRetentionOptions Retention { get; init; } = new();
}

/// <summary>
/// Retention policy targets for each category of persisted data.
/// </summary>
public sealed class TracksideRetentionOptions
{
    /// <summary>
    /// Detailed per-driver/per-session lap records are small, but mostly operational after recent sessions.
    /// </summary>
    public int DetailedLapRecordsDays { get; init; } = 35;

    /// <summary>
    /// Session summaries are useful for month/season review and operational troubleshooting.
    /// </summary>
    public int SessionSummariesDays { get; init; } = 730;

    /// <summary>
    /// Track best records should be retained indefinitely unless staff deliberately resets or corrects them.
    /// </summary>
    public int? TrackBestRecordsDays { get; init; }

    /// <summary>
    /// Monthly track periods are business records for displays and should be retained indefinitely by default.
    /// </summary>
    public int? MonthlyTrackPeriodsDays { get; init; }

    /// <summary>
    /// Future high-volume telemetry samples should be short-lived by default.
    /// </summary>
    public int TelemetrySamplesDays { get; init; } = 3;
}