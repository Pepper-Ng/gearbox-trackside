using Trackside.Application.Configuration;
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
    /// Returns recent persisted sessions for the staff browser.
    /// </summary>
    /// <param name="query">Session list filters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recent sessions ordered from newest to oldest.</returns>
    Task<IReadOnlyList<HistoricalSessionSummary>> GetHistoricalSessionsAsync(HistoricalSessionQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one persisted session with participant rows.
    /// </summary>
    /// <param name="sessionId">Durable session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Session detail or null when no matching session exists.</returns>
    Task<HistoricalSessionDetail?> GetHistoricalSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Updates whether a persisted session counts for historical boards.
    /// </summary>
    /// <param name="sessionId">Durable session identifier.</param>
    /// <param name="countForHistory">True when the session should be included in historical boards.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when a session was updated.</returns>
    Task<bool> SetSessionCountForHistoryAsync(string sessionId, bool countForHistory, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a persisted historical session and its short-lived child records.
    /// </summary>
    /// <param name="sessionId">Durable session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True when a session was deleted.</returns>
    Task<bool> DeleteHistoricalSessionAsync(string sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes placeholder historical sessions that have no persisted participants or no known track.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of placeholder sessions deleted.</returns>
    Task<int> DeleteEmptyHistoricalSessionsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Applies a staff correction or exclusion to a persisted participant.
    /// </summary>
    /// <param name="sessionId">Durable session identifier.</param>
    /// <param name="participantId">Durable participant row identifier.</param>
    /// <param name="request">Correction values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated session detail or null when no matching participant exists.</returns>
    Task<HistoricalSessionDetail?> CorrectParticipantAsync(string sessionId, long participantId, ParticipantCorrectionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Applies a staff correction or invalidation to a persisted lap.
    /// </summary>
    /// <param name="sessionId">Durable session identifier.</param>
    /// <param name="lapId">Durable lap row identifier.</param>
    /// <param name="request">Correction values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated session detail or null when no matching lap exists.</returns>
    Task<HistoricalSessionDetail?> CorrectLapAsync(string sessionId, long lapId, LapCorrectionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Enforces configured retention policy without deleting long-lived derived leaderboard records.
    /// </summary>
    /// <param name="retention">Retention settings.</param>
    /// <param name="nowUtc">UTC time used to calculate cutoffs.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Cleanup counts by data category.</returns>
    Task<TracksideRetentionCleanupResult> EnforceRetentionAsync(TracksideRetentionOptions retention, DateTimeOffset nowUtc, CancellationToken cancellationToken);

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
    /// Optional exact vehicle/content name filter.
    /// </summary>
    public string? VehicleName { get; init; }

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
/// Filter values for the staff session browser.
/// </summary>
public sealed record HistoricalSessionQuery
{
    /// <summary>
    /// Maximum number of recent sessions to return.
    /// </summary>
    public int Limit { get; init; } = 50;
}

/// <summary>
/// Persisted session summary used by the staff session browser.
/// </summary>
public record HistoricalSessionSummary
{
    /// <summary>
    /// Durable session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Source that produced the session.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Track name associated with the session.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Session kind associated with the result.
    /// </summary>
    public SessionKind SessionKind { get; init; }

    /// <summary>
    /// Latest observed session phase.
    /// </summary>
    public SessionPhase SessionPhase { get; init; }

    /// <summary>
    /// First time Trackside observed this session.
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; init; }

    /// <summary>
    /// Most recent time Trackside observed this session.
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; }

    /// <summary>
    /// Latest observed vehicle count.
    /// </summary>
    public int VehicleCount { get; init; }

    /// <summary>
    /// True when this session contributes to historical boards.
    /// </summary>
    public bool CountForHistory { get; init; }

    /// <summary>
    /// Number of persisted participants in this session.
    /// </summary>
    public int ParticipantCount { get; init; }

    /// <summary>
    /// Number of persisted completed laps in this session.
    /// </summary>
    public int LapCount { get; init; }

    /// <summary>
    /// Number of persisted laps that count for timing boards.
    /// </summary>
    public int ValidTimedLapCount { get; init; }

    /// <summary>
    /// Fastest valid timed lap in the session, when any.
    /// </summary>
    public double? BestLapSeconds { get; init; }
}

/// <summary>
/// Persisted session detail with participant rows for staff review.
/// </summary>
public sealed record HistoricalSessionDetail : HistoricalSessionSummary
{
    /// <summary>
    /// Participants observed in this session.
    /// </summary>
    public IReadOnlyList<HistoricalSessionParticipant> Participants { get; init; } = [];
}

/// <summary>
/// Participant row in a persisted session detail.
/// </summary>
public sealed record HistoricalSessionParticipant
{
    /// <summary>
    /// Durable participant row identifier.
    /// </summary>
    public long ParticipantId { get; init; }

    /// <summary>
    /// Latest display rank for the participant.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Fixed rig or rFactor 2 name.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Screen name captured for this participant.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Staff-entered corrected display name, when it differs from the captured source value.
    /// </summary>
    public string? DisplayNameOverride { get; init; }

    /// <summary>
    /// Effective display name used by historical boards after corrections.
    /// </summary>
    public string EffectiveDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional linked driver profile id.
    /// </summary>
    public string? DriverProfileId { get; init; }

    /// <summary>
    /// Vehicle name captured for this participant.
    /// </summary>
    public string VehicleName { get; init; } = string.Empty;

    /// <summary>
    /// First time Trackside observed this participant in the session.
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; init; }

    /// <summary>
    /// Most recent time Trackside observed this participant in the session.
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; }

    /// <summary>
    /// Latest completed lap count.
    /// </summary>
    public int CompletedLaps { get; init; }

    /// <summary>
    /// Best valid timed lap in seconds when available.
    /// </summary>
    public double? BestLapSeconds { get; init; }

    /// <summary>
    /// Most recently completed lap time in seconds when available.
    /// </summary>
    public double? LastLapSeconds { get; init; }

    /// <summary>
    /// Number of persisted completed laps for this participant.
    /// </summary>
    public int LapCount { get; init; }

    /// <summary>
    /// Number of persisted completed laps that count for timing boards.
    /// </summary>
    public int ValidTimedLapCount { get; init; }

    /// <summary>
    /// True when staff excluded this participant from historical boards.
    /// </summary>
    public bool ExcludedFromHistory { get; init; }

    /// <summary>
    /// Optional staff reason for the current participant correction.
    /// </summary>
    public string? CorrectionReason { get; init; }

    /// <summary>
    /// UTC timestamp of the most recent staff correction.
    /// </summary>
    public DateTimeOffset? CorrectedUtc { get; init; }

    /// <summary>
    /// Persisted completed laps for this participant.
    /// </summary>
    public IReadOnlyList<HistoricalSessionLap> Laps { get; init; } = [];
}

/// <summary>
/// Persisted lap row shown in session detail and used by staff correction controls.
/// </summary>
public sealed record HistoricalSessionLap
{
    /// <summary>
    /// Durable lap row identifier.
    /// </summary>
    public long LapId { get; init; }

    /// <summary>
    /// Durable participant row identifier.
    /// </summary>
    public long ParticipantId { get; init; }

    /// <summary>
    /// Completed lap number within the session.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Captured lap time in seconds.
    /// </summary>
    public double LapSeconds { get; init; }

    /// <summary>
    /// Staff-entered corrected lap time in seconds, when any.
    /// </summary>
    public double? LapSecondsOverride { get; init; }

    /// <summary>
    /// Effective lap time after staff correction.
    /// </summary>
    public double EffectiveLapSeconds { get; init; }

    /// <summary>
    /// rFactor 2 valid-lap flag captured for this lap.
    /// </summary>
    public int? ValidLapFlag { get; init; }

    /// <summary>
    /// True when rFactor 2 considers the lap valid.
    /// </summary>
    public bool IsValidLap { get; init; }

    /// <summary>
    /// True when the lap is valid for timing before staff corrections.
    /// </summary>
    public bool IsValidTimedLap { get; init; }

    /// <summary>
    /// True when staff invalidated this lap.
    /// </summary>
    public bool StaffInvalidated { get; init; }

    /// <summary>
    /// True when this lap is currently eligible for historical timing boards.
    /// </summary>
    public bool CountsForTiming { get; init; }

    /// <summary>
    /// Optional staff reason for the current lap correction.
    /// </summary>
    public string? CorrectionReason { get; init; }

    /// <summary>
    /// UTC timestamp of the most recent staff correction.
    /// </summary>
    public DateTimeOffset? CorrectedUtc { get; init; }

    /// <summary>
    /// UTC timestamp when Trackside observed this lap.
    /// </summary>
    public DateTimeOffset ObservedUtc { get; init; }
}

/// <summary>
/// Staff correction request for a persisted participant.
/// </summary>
public sealed record ParticipantCorrectionRequest
{
    /// <summary>
    /// Corrected display name. Blank or null clears the correction.
    /// </summary>
    public string? DisplayNameOverride { get; init; }

    /// <summary>
    /// True when this participant should be excluded from historical boards.
    /// </summary>
    public bool ExcludedFromHistory { get; init; }

    /// <summary>
    /// Optional staff reason for the correction.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Staff correction request for a persisted lap.
/// </summary>
public sealed record LapCorrectionRequest
{
    /// <summary>
    /// Corrected lap time in seconds. Null clears the correction.
    /// </summary>
    public double? LapSecondsOverride { get; init; }

    /// <summary>
    /// True when staff invalidated this lap.
    /// </summary>
    public bool StaffInvalidated { get; init; }

    /// <summary>
    /// Optional staff reason for the correction.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Counts produced by retention cleanup enforcement.
/// </summary>
public sealed record TracksideRetentionCleanupResult
{
    /// <summary>
    /// Detailed lap rows deleted.
    /// </summary>
    public int DetailedLapRecordsDeleted { get; init; }

    /// <summary>
    /// Session summary rows deleted.
    /// </summary>
    public int SessionSummariesDeleted { get; init; }

    /// <summary>
    /// Derived track-best rows deleted.
    /// </summary>
    public int TrackBestRecordsDeleted { get; init; }

    /// <summary>
    /// Monthly track period rows deleted.
    /// </summary>
    public int MonthlyTrackPeriodsDeleted { get; init; }
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