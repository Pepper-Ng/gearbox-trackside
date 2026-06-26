using Trackside.Domain.LiveSession;

namespace Trackside.Application.Persistence;

/// <summary>
/// Durable Phase 2 store for staff aliases, observed sessions, participants, and historical board summaries.
/// </summary>
public interface ITracksideStore
{
    /// <summary>
    /// True when the store should be used instead of Phase 1 configuration-backed fallbacks.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Provider-neutral status for diagnostics and admin-only health surfaces.
    /// </summary>
    TracksideStoreStatus Status { get; }

    /// <summary>
    /// Creates or upgrades the durable schema.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes after the schema is ready.</returns>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Seeds aliases from legacy configuration when the durable alias table is empty.
    /// </summary>
    /// <param name="aliases">Initial alias map keyed by rig name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes after seeding is considered.</returns>
    Task SeedDriverAliasesAsync(IReadOnlyDictionary<string, string> aliases, CancellationToken cancellationToken);

    /// <summary>
    /// Returns current staff aliases keyed by rig name.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current alias map.</returns>
    Task<IReadOnlyDictionary<string, string>> GetDriverAliasesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns current prepared rig setup entries used for future sessions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Prepared setup entries.</returns>
    Task<IReadOnlyList<PreparedSessionEntry>> GetPreparedSessionEntriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// True after staff has explicitly saved or cleared prepared session setup.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when setup has been configured by staff.</returns>
    Task<bool> IsPreparedSessionSetupConfiguredAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the prepared rig setup entries used for future sessions.
    /// </summary>
    /// <param name="entries">Prepared setup entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes after entries are saved.</returns>
    Task SavePreparedSessionEntriesAsync(IReadOnlyList<PreparedSessionEntryRequest> entries, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the prepared rig setup entries for future sessions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes after setup entries are cleared.</returns>
    Task ClearPreparedSessionEntriesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Replaces the current staff alias map.
    /// </summary>
    /// <param name="aliases">Alias map keyed by rig name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes after aliases are saved.</returns>
    Task SaveDriverAliasesAsync(IReadOnlyDictionary<string, string> aliases, CancellationToken cancellationToken);

    /// <summary>
    /// Returns optional recurring-customer driver profiles.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Driver profiles.</returns>
    Task<IReadOnlyList<DriverProfile>> GetDriverProfilesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Creates a driver profile for a recurring customer.
    /// </summary>
    /// <param name="request">Profile values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created profile.</returns>
    Task<DriverProfile> CreateDriverProfileAsync(DriverProfileRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Persists the latest live-session snapshot into the historical-board foundation tables.
    /// </summary>
    /// <param name="snapshot">Snapshot to persist.</param>
    /// <param name="countForHistory">True when the observed session should count for historical boards by default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes after the snapshot is saved.</returns>
    Task SaveLiveSessionSnapshotAsync(LiveSessionSnapshot snapshot, bool countForHistory, CancellationToken cancellationToken);

    /// <summary>
    /// Returns fastest persisted laps for the first historical board queries.
    /// </summary>
    /// <param name="query">Board query filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Fastest persisted laps ordered by lap time.</returns>
    Task<IReadOnlyList<HistoricalBestLap>> GetBestLapsAsync(HistoricalBestLapQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the most recently finished session result, if Trackside has observed one.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Last finished session result or null.</returns>
    Task<FinishedSessionResult?> GetLastFinishedSessionResultAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Returns the active monthly track period, if one has been started by staff.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Active monthly track period or null.</returns>
    Task<MonthlyTrackPeriod?> GetActiveMonthlyTrackAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Starts a fresh monthly track period, closing any previous active period.
    /// </summary>
    /// <param name="trackName">Track name for the new period.</param>
    /// <param name="startedUtc">UTC start timestamp for the new period.</param>
    /// <param name="reason">Optional admin reason, such as scheduled rotation or reset.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly active monthly track period.</returns>
    Task<MonthlyTrackPeriod> StartMonthlyTrackAsync(string trackName, DateTimeOffset startedUtc, string? reason, CancellationToken cancellationToken);
}

/// <summary>
/// Filter values for historical fastest-lap queries.
/// </summary>
public sealed record HistoricalBestLapQuery
{
    /// <summary>
    /// Inclusive lower bound for observed session time.
    /// </summary>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>
    /// Exclusive upper bound for observed session time.
    /// </summary>
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>
    /// Optional exact track name filter.
    /// </summary>
    public string? TrackName { get; init; }

    /// <summary>
    /// Optional session kind filter.
    /// </summary>
    public SessionKind? SessionKind { get; init; }

    /// <summary>
    /// Ranking behavior for best-lap boards.
    /// </summary>
    public BestLapBoardMode Mode { get; init; } = BestLapBoardMode.PerDriver;

    /// <summary>
    /// True when results should be ordered by track name and then lap time.
    /// </summary>
    public bool SortByTrack { get; init; }

    /// <summary>
    /// Maximum number of rows to return.
    /// </summary>
    public int Limit { get; init; } = 50;
}

/// <summary>
/// Historical fastest-lap row produced from persisted session summaries.
/// </summary>
public sealed record HistoricalBestLap
{
    /// <summary>
    /// Durable session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Completed lap number within the session.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Track name associated with the session.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Session kind associated with the result.
    /// </summary>
    public SessionKind SessionKind { get; init; }

    /// <summary>
    /// Underlying fixed rig name.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Staff-facing display name captured for the result.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Vehicle name associated with the result.
    /// </summary>
    public string VehicleName { get; init; } = string.Empty;

    /// <summary>
    /// Best lap time in seconds.
    /// </summary>
    public double BestLapSeconds { get; init; }

    /// <summary>
    /// rFactor 2 valid-lap flag captured for this lap.
    /// </summary>
    public int? ValidLapFlag { get; init; }

    /// <summary>
    /// True when rFactor 2 considers the completed lap valid.
    /// </summary>
    public bool IsValidLap { get; init; }

    /// <summary>
    /// True when rFactor 2 considers the completed lap time valid for timing boards.
    /// </summary>
    public bool IsValidTimedLap { get; init; }

    /// <summary>
    /// First time Trackside observed this session.
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; init; }

    /// <summary>
    /// Most recent time Trackside observed this session.
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; }

    /// <summary>
    /// Timestamp when this lap record was observed.
    /// </summary>
    public DateTimeOffset ObservedUtc { get; init; }
}

/// <summary>
/// Active monthly venue track period. Starting a new period resets the displayed monthly board without deleting old lap records.
/// </summary>
public sealed record MonthlyTrackPeriod
{
    /// <summary>
    /// Durable period identifier.
    /// </summary>
    public string PeriodId { get; init; } = string.Empty;

    /// <summary>
    /// Track name for the active monthly challenge.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// UTC timestamp when this period started.
    /// </summary>
    public DateTimeOffset StartedUtc { get; init; }

    /// <summary>
    /// UTC timestamp when this period ended, or null when active.
    /// </summary>
    public DateTimeOffset? EndedUtc { get; init; }

    /// <summary>
    /// Optional admin reason, such as scheduled rotation or reset.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Provider-neutral persistence status. Implementations may use SQLite, MySQL, or another backend behind this contract.
/// </summary>
public sealed record TracksideStoreStatus
{
    /// <summary>
    /// Human-readable provider name, for example SQLite.
    /// </summary>
    public string Provider { get; init; } = "None";

    /// <summary>
    /// True when durable persistence is active.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Admin-only display location, such as a file path or database name. Must not contain passwords or secrets.
    /// </summary>
    public string? DisplayLocation { get; init; }
}

/// <summary>
/// Ranking mode for best-lap boards.
/// </summary>
public enum BestLapBoardMode
{
    /// <summary>
    /// One row per driver identity, using that driver's fastest valid timed lap.
    /// </summary>
    PerDriver,

    /// <summary>
    /// Every valid timed lap can appear independently.
    /// </summary>
    AllLaps,
}

/// <summary>
/// Optional recurring-customer profile. Display boards should not infer identity from this unless the venue chooses that workflow.
/// </summary>
public sealed record DriverProfile
{
    /// <summary>
    /// Durable profile identifier.
    /// </summary>
    public string DriverProfileId { get; init; } = string.Empty;

    /// <summary>
    /// Staff-facing profile display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional email address for future result/report delivery.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Optional staff notes.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// UTC timestamp when the profile was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>
    /// UTC timestamp when the profile was last updated.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; init; }
}

/// <summary>
/// Request to create a recurring-customer driver profile.
/// </summary>
public sealed record DriverProfileRequest
{
    /// <summary>
    /// Staff-facing profile display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional email address for future result/report delivery.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Optional staff notes.
    /// </summary>
    public string? Notes { get; init; }
}

/// <summary>
/// Prepared rig assignment used for future sessions until staff changes or clears it.
/// </summary>
public sealed record PreparedSessionEntry
{
    /// <summary>
    /// Fixed rig or rFactor 2 name, such as Setup1.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Screen name staff assigned to that rig.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional linked recurring-customer profile.
    /// </summary>
    public string? DriverProfileId { get; init; }

    /// <summary>
    /// Display name of the linked profile, when present.
    /// </summary>
    public string? DriverProfileDisplayName { get; init; }

    /// <summary>
    /// UTC timestamp when this setup entry was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>
    /// UTC timestamp when this setup entry was last updated.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; init; }
}

/// <summary>
/// Request to save a prepared rig assignment.
/// </summary>
public sealed record PreparedSessionEntryRequest
{
    /// <summary>
    /// Fixed rig or rFactor 2 name, such as Setup1.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Screen name staff assigned to that rig.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional linked recurring-customer profile.
    /// </summary>
    public string? DriverProfileId { get; init; }
}

/// <summary>
/// Finished session result overview.
/// </summary>
public sealed record FinishedSessionResult
{
    /// <summary>
    /// Durable session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Track name associated with the session.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Session kind associated with the result.
    /// </summary>
    public SessionKind SessionKind { get; init; }

    /// <summary>
    /// First time Trackside observed the session.
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; init; }

    /// <summary>
    /// Last time Trackside observed the session.
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; }

    /// <summary>
    /// Result rows ordered by latest rank/position.
    /// </summary>
    public IReadOnlyList<FinishedSessionResultRow> Rows { get; init; } = [];
}

/// <summary>
/// One row in a finished session result overview.
/// </summary>
public sealed record FinishedSessionResultRow
{
    /// <summary>
    /// One-based display rank.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Screen name captured for this participant.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Fixed rig or rFactor 2 name.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Optional linked driver profile id.
    /// </summary>
    public string? DriverProfileId { get; init; }

    /// <summary>
    /// Vehicle name captured for this participant.
    /// </summary>
    public string VehicleName { get; init; } = string.Empty;

    /// <summary>
    /// Completed lap count.
    /// </summary>
    public int CompletedLaps { get; init; }

    /// <summary>
    /// Best valid timed lap in seconds when available.
    /// </summary>
    public double? BestLapSeconds { get; init; }
}