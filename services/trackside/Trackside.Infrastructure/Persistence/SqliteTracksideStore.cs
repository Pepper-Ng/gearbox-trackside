using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Trackside.Application.Configuration;
using Trackside.Application.Persistence;
using Trackside.Domain.LiveSession;

namespace Trackside.Infrastructure.Persistence;

/// <summary>
/// SQLite-backed Phase 2 store for aliases, observed sessions, participants, sectors, and summary results.
/// </summary>
public sealed class SqliteTracksideStore : ITracksideStore
{
    private const int SchemaVersion = 2;
    private const string LegacyAliasSeededKey = "legacy_alias_seeded";
    private const string PreparedSessionSetupConfiguredKey = "prepared_session_setup_configured";
    private static readonly TimeSpan SessionStartBucket = TimeSpan.FromMinutes(5);
    private readonly SqliteTracksideStoreOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _initialized;

    /// <summary>
    /// Creates a SQLite Trackside store.
    /// </summary>
    /// <param name="options">Resolved store options.</param>
    /// <param name="timeProvider">Clock used for durable timestamps.</param>
    public SqliteTracksideStore(SqliteTracksideStoreOptions options, TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public bool IsEnabled => _options.Enabled;

    /// <inheritdoc />
    public TracksideStoreStatus Status => new()
    {
        Provider = "SQLite",
        IsEnabled = IsEnabled,
        DisplayLocation = IsEnabled ? _options.DatabasePath : null,
    };

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled || _initialized)
        {
            return;
        }

        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_options.DatabasePath) ?? AppContext.BaseDirectory);
            await using var connection = await OpenConnectionAsync(cancellationToken);
            await ExecuteAsync(connection, SchemaSql, cancellationToken);
            await ApplyMigrationsAsync(connection, cancellationToken);

            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    /// <inheritdoc />
    public async Task SeedDriverAliasesAsync(IReadOnlyDictionary<string, string> aliases, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        if (await HasMetadataAsync(connection, LegacyAliasSeededKey, cancellationToken))
        {
            return;
        }

        using var transaction = connection.BeginTransaction();
        await ReplaceDriverAliasesAsync(connection, transaction, aliases, cancellationToken);
        await SetMetadataAsync(connection, transaction, LegacyAliasSeededKey, "true", cancellationToken);
        await SetMetadataAsync(connection, transaction, PreparedSessionSetupConfiguredKey, "true", cancellationToken);
        transaction.Commit();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetDriverAliasesAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT rig_name, display_name
            FROM driver_aliases
            ORDER BY rig_name;
            """;

        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            aliases[reader.GetString(0)] = reader.GetString(1);
        }

        return aliases;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PreparedSessionEntry>> GetPreparedSessionEntriesAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return [];
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT driver_aliases.rig_name,
                   driver_aliases.display_name,
                   driver_aliases.driver_profile_id,
                   driver_profiles.display_name,
                   driver_aliases.created_utc,
                   driver_aliases.updated_utc
            FROM driver_aliases
            LEFT JOIN driver_profiles ON driver_profiles.driver_profile_id = driver_aliases.driver_profile_id
            ORDER BY driver_aliases.rig_name;
            """;

        var entries = new List<PreparedSessionEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(new PreparedSessionEntry
            {
                RigName = reader.GetString(0),
                DisplayName = reader.GetString(1),
                DriverProfileId = reader.IsDBNull(2) ? null : reader.GetString(2),
                DriverProfileDisplayName = reader.IsDBNull(3) ? null : reader.GetString(3),
                CreatedUtc = ParseDateTime(reader.GetString(4)),
                UpdatedUtc = ParseDateTime(reader.GetString(5)),
            });
        }

        return entries;
    }

    /// <inheritdoc />
    public async Task<bool> IsPreparedSessionSetupConfiguredAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return false;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        return await HasMetadataAsync(connection, PreparedSessionSetupConfiguredKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SavePreparedSessionEntriesAsync(IReadOnlyList<PreparedSessionEntryRequest> entries, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await ReplacePreparedSessionEntriesAsync(connection, transaction, entries, cancellationToken);
        await SetMetadataAsync(connection, transaction, LegacyAliasSeededKey, "true", cancellationToken);
        await SetMetadataAsync(connection, transaction, PreparedSessionSetupConfiguredKey, "true", cancellationToken);
        transaction.Commit();
    }

    /// <inheritdoc />
    public async Task ClearPreparedSessionEntriesAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await ExecuteAsync(connection, "DELETE FROM driver_aliases;", cancellationToken, transaction);
        await SetMetadataAsync(connection, transaction, LegacyAliasSeededKey, "true", cancellationToken);
        await SetMetadataAsync(connection, transaction, PreparedSessionSetupConfiguredKey, "true", cancellationToken);
        transaction.Commit();
    }

    /// <inheritdoc />
    public async Task SaveDriverAliasesAsync(IReadOnlyDictionary<string, string> aliases, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await ReplaceDriverAliasesAsync(connection, transaction, aliases, cancellationToken);
        await SetMetadataAsync(connection, transaction, LegacyAliasSeededKey, "true", cancellationToken);
        await SetMetadataAsync(connection, transaction, PreparedSessionSetupConfiguredKey, "true", cancellationToken);
        transaction.Commit();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DriverProfile>> GetDriverProfilesAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return [];
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT driver_profile_id, display_name, email, notes, created_utc, updated_utc
            FROM driver_profiles
            ORDER BY display_name;
            """;

        var profiles = new List<DriverProfile>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            profiles.Add(ReadDriverProfile(reader));
        }

        return profiles;
    }

    /// <inheritdoc />
    public async Task<DriverProfile> CreateDriverProfileAsync(DriverProfileRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("Driver profile display name is required.", nameof(request));
        }

        await InitializeAsync(cancellationToken);
        var profile = new DriverProfile
        {
            DriverProfileId = Guid.NewGuid().ToString("N"),
            DisplayName = request.DisplayName.Trim(),
            Email = NullIfWhiteSpace(request.Email),
            Notes = NullIfWhiteSpace(request.Notes),
            CreatedUtc = _timeProvider.GetUtcNow(),
            UpdatedUtc = _timeProvider.GetUtcNow(),
        };

        await using var connection = await OpenConnectionAsync(cancellationToken);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO driver_profiles (driver_profile_id, display_name, email, notes, created_utc, updated_utc)
            VALUES ($driverProfileId, $displayName, $email, $notes, $createdUtc, $updatedUtc);
            """,
            cancellationToken,
            parameters:
            [
                ("$driverProfileId", profile.DriverProfileId),
                ("$displayName", profile.DisplayName),
                ("$email", DbValue(profile.Email)),
                ("$notes", DbValue(profile.Notes)),
                ("$createdUtc", FormatDateTime(profile.CreatedUtc)),
                ("$updatedUtc", FormatDateTime(profile.UpdatedUtc)),
            ]);

        return profile;
    }

    /// <inheritdoc />
    public async Task SaveLiveSessionSnapshotAsync(LiveSessionSnapshot snapshot, bool countForHistory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!IsEnabled)
        {
            return;
        }

        if (!ShouldPersistSnapshot(snapshot))
        {
            return;
        }

        await InitializeAsync(cancellationToken);
        var observedUtc = snapshot.TimestampUtc == DateTimeOffset.UnixEpoch
            ? _timeProvider.GetUtcNow()
            : snapshot.TimestampUtc.ToUniversalTime();
        var sessionId = BuildSessionId(snapshot, observedUtc);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await UpsertSessionAsync(connection, transaction, sessionId, snapshot, observedUtc, countForHistory, cancellationToken);
        foreach (var driver in snapshot.Drivers)
        {
            var driverId = NormalizeDriverId(driver);
            var previousCompletedLaps = await GetPreviousCompletedLapsAsync(connection, transaction, sessionId, driverId, cancellationToken);
            var driverProfileId = await GetPreparedDriverProfileIdAsync(connection, transaction, driver.RigName, cancellationToken);
            var participantId = await UpsertParticipantAsync(connection, transaction, sessionId, driver, driverProfileId, observedUtc, cancellationToken);
            await InsertCompletedLapAsync(connection, transaction, participantId, driver, previousCompletedLaps, observedUtc, cancellationToken);
            await UpsertSectorsAsync(connection, transaction, participantId, driver, observedUtc, cancellationToken);
            await UpsertSummaryResultAsync(connection, transaction, sessionId, participantId, driver, observedUtc, cancellationToken);
            await RefreshTrackBestRecordsForParticipantAsync(connection, transaction, participantId, cancellationToken);
        }

        transaction.Commit();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoricalBestLap>> GetBestLapsAsync(HistoricalBestLapQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!IsEnabled)
        {
            return [];
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        var where = new StringBuilder("WHERE count_for_history = 1 AND participant_excluded = 0 AND staff_invalidated = 0 AND is_valid_timed_lap = 1");

        if (query.FromUtc is not null)
        {
            where.Append(" AND observed_utc >= $fromUtc");
            command.Parameters.AddWithValue("$fromUtc", FormatDateTime(query.FromUtc.Value));
        }

        if (query.ToUtc is not null)
        {
            where.Append(" AND observed_utc < $toUtc");
            command.Parameters.AddWithValue("$toUtc", FormatDateTime(query.ToUtc.Value));
        }

        if (!string.IsNullOrWhiteSpace(query.TrackName))
        {
            where.Append(" AND track_name = $trackName");
            command.Parameters.AddWithValue("$trackName", query.TrackName.Trim());
        }

        if (query.SessionKind is not null)
        {
            where.Append(" AND session_kind = $sessionKind");
            command.Parameters.AddWithValue("$sessionKind", query.SessionKind.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(query.VehicleName))
        {
            where.Append(" AND vehicle_name = $vehicleName");
            command.Parameters.AddWithValue("$vehicleName", query.VehicleName.Trim());
        }

        command.CommandText = $$"""
            WITH eligible AS (
                SELECT session_id,
                       track_name,
                       session_kind,
                       lap_number,
                       rig_name,
                       display_name,
                       vehicle_name,
                       lap_time_seconds,
                       valid_lap_flag,
                       is_valid_lap,
                       is_valid_timed_lap,
                       first_seen_utc,
                       last_seen_utc,
                       observed_utc,
                       lower(trim(coalesce(nullif(display_name, ''), rig_name))) AS driver_key
                FROM track_best_records
                {{where}}
            ), ranked AS (
                SELECT *,
                       ROW_NUMBER() OVER (PARTITION BY track_name, driver_key ORDER BY lap_time_seconds ASC, observed_utc ASC) AS driver_lap_rank
                FROM eligible
            )
            SELECT session_id,
                   track_name,
                   session_kind,
                   lap_number,
                   rig_name,
                   display_name,
                   vehicle_name,
                   lap_time_seconds,
                   valid_lap_flag,
                   is_valid_lap,
                   is_valid_timed_lap,
                   first_seen_utc,
                   last_seen_utc,
                   observed_utc
            FROM ranked
            WHERE $mode = 'all-laps' OR driver_lap_rank = 1
            ORDER BY {{(query.SortByTrack ? "track_name ASC, " : string.Empty)}}lap_time_seconds ASC, observed_utc ASC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(query.Limit, 1, 500));
        command.Parameters.AddWithValue("$mode", query.Mode == BestLapBoardMode.AllLaps ? "all-laps" : "per-driver");

        var results = new List<HistoricalBestLap>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new HistoricalBestLap
            {
                SessionId = reader.GetString(0),
                TrackName = reader.GetString(1),
                SessionKind = ParseSessionKind(reader.GetString(2)),
                LapNumber = reader.GetInt32(3),
                RigName = reader.GetString(4),
                DisplayName = reader.GetString(5),
                VehicleName = reader.GetString(6),
                BestLapSeconds = reader.GetDouble(7),
                ValidLapFlag = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                IsValidLap = reader.GetInt32(9) == 1,
                IsValidTimedLap = reader.GetInt32(10) == 1,
                FirstSeenUtc = ParseDateTime(reader.GetString(11)),
                LastSeenUtc = ParseDateTime(reader.GetString(12)),
                ObservedUtc = ParseDateTime(reader.GetString(13)),
            });
        }

        return results;
    }

    /// <inheritdoc />
    public async Task<FinishedSessionResult?> GetLastFinishedSessionResultAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var sessionCommand = connection.CreateCommand();
        sessionCommand.CommandText = """
            SELECT session_id, track_name, session_kind, first_seen_utc, last_seen_utc
            FROM sessions
            WHERE session_phase = 'SessionOver'
            ORDER BY last_seen_utc DESC
            LIMIT 1;
            """;

        await using var sessionReader = await sessionCommand.ExecuteReaderAsync(cancellationToken);
        if (!await sessionReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var sessionId = sessionReader.GetString(0);
        var result = new FinishedSessionResult
        {
            SessionId = sessionId,
            TrackName = sessionReader.GetString(1),
            SessionKind = ParseSessionKind(sessionReader.GetString(2)),
            FirstSeenUtc = ParseDateTime(sessionReader.GetString(3)),
            LastSeenUtc = ParseDateTime(sessionReader.GetString(4)),
        };

        await sessionReader.DisposeAsync();
        await using var rowsCommand = connection.CreateCommand();
        rowsCommand.CommandText = """
             SELECT coalesce(nullif(display_name_override, ''), display_name),
                   rig_name,
                   driver_profile_id,
                   vehicle_name,
                   completed_laps,
                 (SELECT MIN(track_best_records.lap_time_seconds)
                  FROM track_best_records
                  WHERE track_best_records.participant_id = participants.participant_id
                 AND track_best_records.count_for_history = 1
                 AND track_best_records.participant_excluded = 0
                 AND track_best_records.staff_invalidated = 0
                 AND track_best_records.is_valid_timed_lap = 1) AS best_lap_seconds,
                   latest_rank,
                   latest_position
            FROM participants
             WHERE session_id = $sessionId AND excluded_from_history = 0
            ORDER BY latest_rank ASC, latest_position ASC, display_name ASC;
            """;
        rowsCommand.Parameters.AddWithValue("$sessionId", sessionId);

        var rows = new List<FinishedSessionResultRow>();
        await using var rowsReader = await rowsCommand.ExecuteReaderAsync(cancellationToken);
        while (await rowsReader.ReadAsync(cancellationToken))
        {
            rows.Add(new FinishedSessionResultRow
            {
                Rank = rows.Count + 1,
                DisplayName = rowsReader.GetString(0),
                RigName = rowsReader.GetString(1),
                DriverProfileId = rowsReader.IsDBNull(2) ? null : rowsReader.GetString(2),
                VehicleName = rowsReader.GetString(3),
                CompletedLaps = rowsReader.GetInt32(4),
                BestLapSeconds = rowsReader.IsDBNull(5) ? null : rowsReader.GetDouble(5),
            });
        }

        return result with { Rows = rows };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistoricalSessionSummary>> GetHistoricalSessionsAsync(HistoricalSessionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!IsEnabled)
        {
            return [];
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            {HistoricalSessionSelectSql}
            ORDER BY sessions.last_seen_utc DESC, sessions.first_seen_utc DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", Math.Clamp(query.Limit, 1, 200));

        var sessions = new List<HistoricalSessionSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            sessions.Add(ReadHistoricalSessionSummary(reader));
        }

        return sessions;
    }

    /// <inheritdoc />
    public async Task<HistoricalSessionDetail?> GetHistoricalSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var sessionCommand = connection.CreateCommand();
        sessionCommand.CommandText = $"""
            {HistoricalSessionSelectSql}
            WHERE sessions.session_id = $sessionId
            LIMIT 1;
            """;
        sessionCommand.Parameters.AddWithValue("$sessionId", sessionId.Trim());

        await using var sessionReader = await sessionCommand.ExecuteReaderAsync(cancellationToken);
        if (!await sessionReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var summary = ReadHistoricalSessionSummary(sessionReader);
        await sessionReader.DisposeAsync();

        await using var participantsCommand = connection.CreateCommand();
        participantsCommand.CommandText = """
            SELECT participants.participant_id,
                   participants.latest_rank,
                   participants.rig_name,
                   participants.display_name,
                 participants.display_name_override,
                   coalesce(nullif(participants.display_name_override, ''), participants.display_name) AS effective_display_name,
                 participants.driver_profile_id,
                   participants.vehicle_name,
                   participants.first_seen_utc,
                   participants.last_seen_utc,
                   participants.completed_laps,
                   participants.best_lap_seconds,
                   participants.last_lap_seconds,
                   (SELECT COUNT(*) FROM laps WHERE laps.participant_id = participants.participant_id) AS lap_count,
                 (SELECT COUNT(*) FROM laps WHERE laps.participant_id = participants.participant_id AND laps.is_valid_timed_lap = 1 AND laps.staff_invalidated = 0) AS valid_timed_lap_count,
                 participants.excluded_from_history,
                 participants.correction_reason,
                 participants.corrected_utc
            FROM participants
            WHERE participants.session_id = $sessionId
            ORDER BY participants.latest_rank ASC, participants.latest_position ASC, participants.display_name ASC;
            """;
        participantsCommand.Parameters.AddWithValue("$sessionId", summary.SessionId);

        var participants = new List<HistoricalSessionParticipant>();
        await using var participantsReader = await participantsCommand.ExecuteReaderAsync(cancellationToken);
        while (await participantsReader.ReadAsync(cancellationToken))
        {
            participants.Add(ReadHistoricalSessionParticipant(participantsReader));
        }

        var laps = await GetSessionLapsAsync(connection, summary.SessionId, cancellationToken);
        var lapsByParticipant = laps.ToLookup(lap => lap.ParticipantId);
        var participantsWithLaps = participants
            .Select(participant => participant with { Laps = lapsByParticipant[participant.ParticipantId].ToList() })
            .ToList();

        return new HistoricalSessionDetail
        {
            SessionId = summary.SessionId,
            Source = summary.Source,
            TrackName = summary.TrackName,
            SessionKind = summary.SessionKind,
            SessionPhase = summary.SessionPhase,
            FirstSeenUtc = summary.FirstSeenUtc,
            LastSeenUtc = summary.LastSeenUtc,
            VehicleCount = summary.VehicleCount,
            CountForHistory = summary.CountForHistory,
            ParticipantCount = summary.ParticipantCount,
            LapCount = summary.LapCount,
            ValidTimedLapCount = summary.ValidTimedLapCount,
            BestLapSeconds = summary.BestLapSeconds,
            Participants = participantsWithLaps,
        };
    }

    /// <inheritdoc />
    public async Task<bool> SetSessionCountForHistoryAsync(string sessionId, bool countForHistory, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE sessions
            SET count_for_history = $countForHistory
            WHERE session_id = $sessionId;
            """;
        command.Parameters.AddWithValue("$countForHistory", countForHistory ? 1 : 0);
        command.Parameters.AddWithValue("$sessionId", sessionId.Trim());
        var updated = await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        if (updated)
        {
            await ExecuteAsync(
                connection,
                """
                UPDATE track_best_records
                SET count_for_history = $countForHistory
                WHERE session_id = $sessionId;
                """,
                cancellationToken,
                parameters:
                [
                    ("$countForHistory", countForHistory ? 1 : 0),
                    ("$sessionId", sessionId.Trim()),
                ]);
        }

        return updated;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteHistoricalSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!IsEnabled || string.IsNullOrWhiteSpace(sessionId))
        {
            return false;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await ExecuteAsync(
            connection,
            "DELETE FROM track_best_records WHERE session_id = $sessionId;",
            cancellationToken,
            transaction,
            ("$sessionId", sessionId.Trim()));
        var deleted = await ExecuteNonQueryAsync(
            connection,
            "DELETE FROM sessions WHERE session_id = $sessionId;",
            cancellationToken,
            transaction,
            ("$sessionId", sessionId.Trim()));
        transaction.Commit();
        return deleted > 0;
    }

    /// <inheritdoc />
    public async Task<int> DeleteEmptyHistoricalSessionsAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return 0;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await ExecuteAsync(
            connection,
            """
            DELETE FROM track_best_records
            WHERE session_id IN (
                SELECT sessions.session_id
                FROM sessions
                WHERE NOT EXISTS (
                    SELECT 1 FROM participants WHERE participants.session_id = sessions.session_id
                )
                   OR NOT EXISTS (
                    SELECT 1
                    FROM laps
                    INNER JOIN participants lap_participants ON lap_participants.participant_id = laps.participant_id
                    WHERE lap_participants.session_id = sessions.session_id
                )
                   OR lower(trim(sessions.track_name)) IN ('unknown', 'unknown track', 'unavailable')
            );
            """,
            cancellationToken,
            transaction);
        var deleted = await ExecuteNonQueryAsync(
            connection,
            """
            DELETE FROM sessions
            WHERE NOT EXISTS (
                SELECT 1 FROM participants WHERE participants.session_id = sessions.session_id
            )
               OR NOT EXISTS (
                SELECT 1
                FROM laps
                INNER JOIN participants lap_participants ON lap_participants.participant_id = laps.participant_id
                WHERE lap_participants.session_id = sessions.session_id
            )
               OR lower(trim(track_name)) IN ('unknown', 'unknown track', 'unavailable');
            """,
            cancellationToken,
            transaction);
        transaction.Commit();
        return deleted;
    }

    /// <inheritdoc />
    public async Task<HistoricalSessionDetail?> CorrectParticipantAsync(
        string sessionId,
        long participantId,
        ParticipantCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsEnabled || string.IsNullOrWhiteSpace(sessionId) || participantId <= 0)
        {
            return null;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var updated = await ExecuteNonQueryAsync(
            connection,
            """
            UPDATE participants
            SET display_name_override = $displayNameOverride,
                excluded_from_history = $excludedFromHistory,
                correction_reason = $reason,
                corrected_utc = $correctedUtc
            WHERE session_id = $sessionId AND participant_id = $participantId;
            """,
            cancellationToken,
            transaction,
            ("$displayNameOverride", DbValue(request.DisplayNameOverride)),
            ("$excludedFromHistory", request.ExcludedFromHistory ? 1 : 0),
            ("$reason", DbValue(request.Reason)),
            ("$correctedUtc", FormatDateTime(_timeProvider.GetUtcNow())),
            ("$sessionId", sessionId.Trim()),
            ("$participantId", participantId));

        if (updated == 0)
        {
            transaction.Rollback();
            return null;
        }

        await RefreshTrackBestRecordsForParticipantAsync(connection, transaction, participantId, cancellationToken);
        transaction.Commit();
        return await GetHistoricalSessionAsync(sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<HistoricalSessionDetail?> CorrectLapAsync(
        string sessionId,
        long lapId,
        LapCorrectionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!IsEnabled || string.IsNullOrWhiteSpace(sessionId) || lapId <= 0)
        {
            return null;
        }

        if (request.LapSecondsOverride is { } lapSecondsOverride && (!double.IsFinite(lapSecondsOverride) || lapSecondsOverride <= 0))
        {
            throw new ArgumentException("Corrected lap time must be a positive finite number.", nameof(request));
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var participantId = await GetLapParticipantIdAsync(connection, transaction, sessionId.Trim(), lapId, cancellationToken);
        if (participantId is null)
        {
            transaction.Rollback();
            return null;
        }

        var updated = await ExecuteNonQueryAsync(
            connection,
            """
            UPDATE laps
            SET lap_time_seconds_override = $lapTimeSecondsOverride,
                staff_invalidated = $staffInvalidated,
                correction_reason = $reason,
                corrected_utc = $correctedUtc
            WHERE lap_id = $lapId;
            """,
            cancellationToken,
            transaction,
            ("$lapTimeSecondsOverride", DbValue(request.LapSecondsOverride)),
            ("$staffInvalidated", request.StaffInvalidated ? 1 : 0),
            ("$reason", DbValue(request.Reason)),
            ("$correctedUtc", FormatDateTime(_timeProvider.GetUtcNow())),
            ("$lapId", lapId));

        if (updated == 0)
        {
            transaction.Rollback();
            return null;
        }

        await RefreshTrackBestRecordsForParticipantAsync(connection, transaction, participantId.Value, cancellationToken);
        transaction.Commit();
        return await GetHistoricalSessionAsync(sessionId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<TracksideRetentionCleanupResult> EnforceRetentionAsync(
        TracksideRetentionOptions retention,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(retention);
        if (!IsEnabled)
        {
            return new TracksideRetentionCleanupResult();
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        var deletedLaps = 0;
        var deletedSessions = 0;
        var deletedTrackBestRecords = 0;
        var deletedMonthlyTrackPeriods = 0;

        if (retention.DetailedLapRecordsDays > 0)
        {
            deletedLaps = await ExecuteNonQueryAsync(
                connection,
                """
                DELETE FROM laps
                                WHERE observed_utc < $cutoffUtc;
                """,
                cancellationToken,
                parameters:
                [
                    ("$cutoffUtc", FormatDateTime(nowUtc.AddDays(-retention.DetailedLapRecordsDays))),
                ]);
        }

        if (retention.SessionSummariesDays > 0)
        {
            deletedSessions = await ExecuteNonQueryAsync(
                connection,
                """
                DELETE FROM sessions
                WHERE last_seen_utc < $cutoffUtc;
                """,
                cancellationToken,
                parameters:
                [
                    ("$cutoffUtc", FormatDateTime(nowUtc.AddDays(-retention.SessionSummariesDays))),
                ]);
        }

        if (retention.TrackBestRecordsDays is > 0)
        {
            deletedTrackBestRecords = await ExecuteNonQueryAsync(
                connection,
                """
                DELETE FROM track_best_records
                WHERE observed_utc < $cutoffUtc;
                """,
                cancellationToken,
                parameters:
                [
                    ("$cutoffUtc", FormatDateTime(nowUtc.AddDays(-retention.TrackBestRecordsDays.Value))),
                ]);
        }

        if (retention.MonthlyTrackPeriodsDays is > 0)
        {
            deletedMonthlyTrackPeriods = await ExecuteNonQueryAsync(
                connection,
                """
                DELETE FROM monthly_track_periods
                WHERE ended_utc IS NOT NULL AND ended_utc < $cutoffUtc;
                """,
                cancellationToken,
                parameters:
                [
                    ("$cutoffUtc", FormatDateTime(nowUtc.AddDays(-retention.MonthlyTrackPeriodsDays.Value))),
                ]);
        }

        return new TracksideRetentionCleanupResult
        {
            DetailedLapRecordsDeleted = deletedLaps,
            SessionSummariesDeleted = deletedSessions,
            TrackBestRecordsDeleted = deletedTrackBestRecords,
            MonthlyTrackPeriodsDeleted = deletedMonthlyTrackPeriods,
        };
    }

    /// <inheritdoc />
    public async Task<MonthlyTrackPeriod?> GetActiveMonthlyTrackAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        await InitializeAsync(cancellationToken);
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT period_id, track_name, started_utc, ended_utc, reason
            FROM monthly_track_periods
            WHERE ended_utc IS NULL
            ORDER BY started_utc DESC
            LIMIT 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadMonthlyTrackPeriod(reader) : null;
    }

    /// <inheritdoc />
    public async Task<MonthlyTrackPeriod> StartMonthlyTrackAsync(
        string trackName,
        DateTimeOffset startedUtc,
        string? reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            throw new ArgumentException("Track name is required.", nameof(trackName));
        }

        await InitializeAsync(cancellationToken);
        var normalizedTrackName = trackName.Trim();
        var normalizedStartUtc = startedUtc.ToUniversalTime();
        var periodId = BuildPeriodId(normalizedTrackName, normalizedStartUtc);

        await using var connection = await OpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        await ExecuteAsync(
            connection,
            """
            UPDATE monthly_track_periods
            SET ended_utc = $endedUtc
            WHERE ended_utc IS NULL;
            """,
            cancellationToken,
            transaction,
            ("$endedUtc", FormatDateTime(normalizedStartUtc)));
        await ExecuteAsync(
            connection,
            """
            INSERT INTO monthly_track_periods (period_id, track_name, started_utc, ended_utc, reason)
            VALUES ($periodId, $trackName, $startedUtc, NULL, $reason);
            """,
            cancellationToken,
            transaction,
            ("$periodId", periodId),
            ("$trackName", normalizedTrackName),
            ("$startedUtc", FormatDateTime(normalizedStartUtc)),
            ("$reason", string.IsNullOrWhiteSpace(reason) ? DBNull.Value : reason.Trim()));
        transaction.Commit();

        return new MonthlyTrackPeriod
        {
            PeriodId = periodId,
            TrackName = normalizedTrackName,
            StartedUtc = normalizedStartUtc,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
        };
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = _options.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        await command.ExecuteNonQueryAsync(cancellationToken);
        return connection;
    }

    private static async Task ApplyMigrationsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var appliedVersions = await GetAppliedMigrationVersionsAsync(connection, cancellationToken);
        if (!appliedVersions.Contains(1))
        {
            await RecordMigrationAsync(connection, 1, cancellationToken);
        }

        if (!appliedVersions.Contains(2))
        {
            await ApplyMigration2Async(connection, cancellationToken);
            await RecordMigrationAsync(connection, 2, cancellationToken);
        }
    }

    private static async Task<HashSet<int>> GetAppliedMigrationVersionsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT version FROM schema_migrations;";
        var versions = new HashSet<int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            versions.Add(reader.GetInt32(0));
        }

        return versions;
    }

    private static async Task RecordMigrationAsync(SqliteConnection connection, int version, CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            connection,
            """
            INSERT OR IGNORE INTO schema_migrations (version, applied_utc)
            VALUES ($version, $appliedUtc);
            """,
            cancellationToken,
            parameters:
            [
                ("$version", version),
                ("$appliedUtc", FormatDateTime(DateTimeOffset.UtcNow)),
            ]);
    }

    private static async Task ApplyMigration2Async(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await AddColumnIfMissingAsync(connection, "laps", "valid_lap_flag", "INTEGER NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "laps", "is_valid_lap", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(connection, "laps", "is_valid_timed_lap", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(connection, "laps", "lap_time_seconds_override", "REAL NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "laps", "staff_invalidated", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(connection, "laps", "correction_reason", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "laps", "corrected_utc", "TEXT NULL", cancellationToken);

        await AddColumnIfMissingAsync(connection, "driver_aliases", "driver_profile_id", "TEXT NULL", cancellationToken);

        await AddColumnIfMissingAsync(connection, "participants", "driver_profile_id", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "participants", "display_name_override", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "participants", "excluded_from_history", "INTEGER NOT NULL DEFAULT 0", cancellationToken);
        await AddColumnIfMissingAsync(connection, "participants", "correction_reason", "TEXT NULL", cancellationToken);
        await AddColumnIfMissingAsync(connection, "participants", "corrected_utc", "TEXT NULL", cancellationToken);

        var refreshedColumns = await GetColumnNamesAsync(connection, "laps", cancellationToken);
        if (refreshedColumns.Contains("count_lap_flag"))
        {
            await ExecuteAsync(connection, "UPDATE laps SET valid_lap_flag = count_lap_flag WHERE valid_lap_flag IS NULL;", cancellationToken);
        }

        if (refreshedColumns.Contains("counts_for_lap"))
        {
            await ExecuteAsync(connection, "UPDATE laps SET is_valid_lap = counts_for_lap WHERE is_valid_lap = 0;", cancellationToken);
        }

        if (refreshedColumns.Contains("counts_for_timing"))
        {
            await ExecuteAsync(connection, "UPDATE laps SET is_valid_timed_lap = counts_for_timing WHERE is_valid_timed_lap = 0;", cancellationToken);
        }

        await ExecuteAsync(connection, TrackBestRecordsSchemaSql, cancellationToken);
        await RefreshAllTrackBestRecordsAsync(connection, cancellationToken);
    }

    private static async Task AddColumnIfMissingAsync(
        SqliteConnection connection,
        string tableName,
        string columnName,
        string columnDefinition,
        CancellationToken cancellationToken)
    {
        var columns = await GetColumnNamesAsync(connection, tableName, cancellationToken);
        if (!columns.Contains(columnName))
        {
            await ExecuteAsync(connection, $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};", cancellationToken);
        }
    }

    private static async Task<HashSet<string>> GetColumnNamesAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({tableName});";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private async Task ReplaceDriverAliasesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyDictionary<string, string> aliases,
        CancellationToken cancellationToken)
    {
        var entries = NormalizeAliases(aliases)
            .Select(alias => new PreparedSessionEntryRequest { RigName = alias.Key, DisplayName = alias.Value })
            .ToList();
        await ReplacePreparedSessionEntriesAsync(connection, transaction, entries, cancellationToken);
    }

    private async Task ReplacePreparedSessionEntriesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<PreparedSessionEntryRequest> entries,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(connection, "DELETE FROM driver_aliases;", cancellationToken, transaction);
        var timestampUtc = FormatDateTime(_timeProvider.GetUtcNow());
        foreach (var entry in NormalizePreparedSessionEntries(entries))
        {
            await ExecuteAsync(
                connection,
                """
                INSERT INTO driver_aliases (rig_name, display_name, driver_profile_id, created_utc, updated_utc)
                VALUES ($rigName, $displayName, $driverProfileId, $createdUtc, $updatedUtc);
                """,
                cancellationToken,
                transaction,
                ("$rigName", entry.RigName),
                ("$displayName", entry.DisplayName),
                ("$driverProfileId", DbValue(entry.DriverProfileId)),
                ("$createdUtc", timestampUtc),
                ("$updatedUtc", timestampUtc));
        }
    }

    private static async Task<bool> HasMetadataAsync(SqliteConnection connection, string key, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM store_metadata WHERE key = $key LIMIT 1;";
        command.Parameters.AddWithValue("$key", key);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static Task SetMetadataAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string key,
        string value,
        CancellationToken cancellationToken) => ExecuteAsync(
            connection,
            """
            INSERT INTO store_metadata (key, value, updated_utc)
            VALUES ($key, $value, $updatedUtc)
            ON CONFLICT(key) DO UPDATE SET
                value = excluded.value,
                updated_utc = excluded.updated_utc;
            """,
            cancellationToken,
            transaction,
            ("$key", key),
            ("$value", value),
            ("$updatedUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)));

    private static async Task UpsertSessionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sessionId,
        LiveSessionSnapshot snapshot,
        DateTimeOffset observedUtc,
        bool countForHistory,
        CancellationToken cancellationToken) => await ExecuteAsync(
            connection,
            """
            INSERT INTO sessions (
                session_id, source, track_name, session_kind, session_phase, first_seen_utc, last_seen_utc,
                scheduled_duration_seconds, lap_distance_meters, vehicle_count, air_temperature_celsius,
                track_temperature_celsius, rain_intensity, cloud_intensity, track_wetness, overall_flag, count_for_history)
            VALUES (
                $sessionId, $source, $trackName, $sessionKind, $sessionPhase, $firstSeenUtc, $lastSeenUtc,
                $scheduledDurationSeconds, $lapDistanceMeters, $vehicleCount, $airTemperatureCelsius,
                $trackTemperatureCelsius, $rainIntensity, $cloudIntensity, $trackWetness, $overallFlag, $countForHistory)
            ON CONFLICT(session_id) DO UPDATE SET
                source = excluded.source,
                track_name = excluded.track_name,
                session_kind = excluded.session_kind,
                session_phase = excluded.session_phase,
                last_seen_utc = excluded.last_seen_utc,
                scheduled_duration_seconds = excluded.scheduled_duration_seconds,
                lap_distance_meters = excluded.lap_distance_meters,
                vehicle_count = excluded.vehicle_count,
                air_temperature_celsius = excluded.air_temperature_celsius,
                track_temperature_celsius = excluded.track_temperature_celsius,
                rain_intensity = excluded.rain_intensity,
                cloud_intensity = excluded.cloud_intensity,
                track_wetness = excluded.track_wetness,
                overall_flag = excluded.overall_flag;
            """,
            cancellationToken,
            transaction,
            ("$sessionId", sessionId),
            ("$source", snapshot.Source),
            ("$trackName", snapshot.Session.TrackName),
            ("$sessionKind", snapshot.Session.Kind.ToString()),
            ("$sessionPhase", snapshot.Session.Phase.ToString()),
            ("$firstSeenUtc", FormatDateTime(observedUtc)),
            ("$lastSeenUtc", FormatDateTime(observedUtc)),
            ("$scheduledDurationSeconds", DbValue(snapshot.Session.ScheduledDurationSeconds)),
            ("$lapDistanceMeters", DbValue(snapshot.Session.LapDistanceMeters)),
            ("$vehicleCount", snapshot.Session.VehicleCount),
            ("$airTemperatureCelsius", DbValue(snapshot.Session.AirTemperatureCelsius)),
            ("$trackTemperatureCelsius", DbValue(snapshot.Session.TrackTemperatureCelsius)),
            ("$rainIntensity", DbValue(snapshot.Session.RainIntensity)),
            ("$cloudIntensity", DbValue(snapshot.Session.CloudIntensity)),
            ("$trackWetness", DbValue(snapshot.Session.TrackWetness)),
            ("$overallFlag", snapshot.Session.OverallFlag),
            ("$countForHistory", countForHistory ? 1 : 0));

    private static async Task<long> UpsertParticipantAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sessionId,
        DriverSnapshot driver,
        string? driverProfileId,
        DateTimeOffset observedUtc,
        CancellationToken cancellationToken)
    {
        var driverId = NormalizeDriverId(driver);
        await ExecuteAsync(
            connection,
            """
            INSERT INTO participants (
                session_id, driver_id, driver_profile_id, rig_name, display_name, vehicle_name, first_seen_utc, last_seen_utc,
                latest_rank, latest_position, completed_laps, best_lap_seconds, last_lap_seconds, current_lap_seconds,
                gap_to_leader_seconds, gap_to_next_seconds, laps_behind_leader, track_position_percent, lap_distance_meters)
            VALUES (
                $sessionId, $driverId, $driverProfileId, $rigName, $displayName, $vehicleName, $firstSeenUtc, $lastSeenUtc,
                $latestRank, $latestPosition, $completedLaps, $bestLapSeconds, $lastLapSeconds, $currentLapSeconds,
                $gapToLeaderSeconds, $gapToNextSeconds, $lapsBehindLeader, $trackPositionPercent, $lapDistanceMeters)
            ON CONFLICT(session_id, driver_id) DO UPDATE SET
                driver_profile_id = excluded.driver_profile_id,
                rig_name = excluded.rig_name,
                display_name = excluded.display_name,
                vehicle_name = excluded.vehicle_name,
                last_seen_utc = excluded.last_seen_utc,
                latest_rank = excluded.latest_rank,
                latest_position = excluded.latest_position,
                completed_laps = excluded.completed_laps,
                best_lap_seconds = excluded.best_lap_seconds,
                last_lap_seconds = excluded.last_lap_seconds,
                current_lap_seconds = excluded.current_lap_seconds,
                gap_to_leader_seconds = excluded.gap_to_leader_seconds,
                gap_to_next_seconds = excluded.gap_to_next_seconds,
                laps_behind_leader = excluded.laps_behind_leader,
                track_position_percent = excluded.track_position_percent,
                lap_distance_meters = excluded.lap_distance_meters;
            """,
            cancellationToken,
            transaction,
            ParticipantParameters(sessionId, driverId, driverProfileId, driver, observedUtc));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT participant_id
            FROM participants
            WHERE session_id = $sessionId AND driver_id = $driverId;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$driverId", driverId);
        return (long)(await command.ExecuteScalarAsync(cancellationToken) ?? 0L);
    }

    private static async Task<int?> GetPreviousCompletedLapsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sessionId,
        string driverId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT completed_laps
            FROM participants
            WHERE session_id = $sessionId AND driver_id = $driverId;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$driverId", driverId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static async Task<string?> GetPreparedDriverProfileIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string rigName,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT driver_profile_id
            FROM driver_aliases
            WHERE rig_name = $rigName;
            """;
        command.Parameters.AddWithValue("$rigName", rigName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null or DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static async Task InsertCompletedLapAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long participantId,
        DriverSnapshot driver,
        int? previousCompletedLaps,
        DateTimeOffset observedUtc,
        CancellationToken cancellationToken)
    {
        if (driver.CompletedLaps <= 0 || driver.LastLapSeconds is not > 0 || previousCompletedLaps >= driver.CompletedLaps)
        {
            return;
        }

        var isValidLap = driver.ValidLapFlag is 1 or 2;
        var isValidTimedLap = driver.ValidLapFlag == 2;
        await ExecuteAsync(
            connection,
            """
            INSERT OR IGNORE INTO laps (
                participant_id,
                lap_number,
                lap_time_seconds,
                valid_lap_flag,
                is_valid_lap,
                is_valid_timed_lap,
                is_best,
                observed_utc
            )
            VALUES (
                $participantId,
                $lapNumber,
                $lapTimeSeconds,
                $validLapFlag,
                $isValidLap,
                $isValidTimedLap,
                $isBest,
                $observedUtc
            );
            """,
            cancellationToken,
            transaction,
            ("$participantId", participantId),
            ("$lapNumber", driver.CompletedLaps),
            ("$lapTimeSeconds", driver.LastLapSeconds.Value),
            ("$validLapFlag", DbValue(driver.ValidLapFlag)),
            ("$isValidLap", isValidLap ? 1 : 0),
            ("$isValidTimedLap", isValidTimedLap ? 1 : 0),
            ("$isBest", IsSameTime(driver.LastLapSeconds, driver.BestLapSeconds) ? 1 : 0),
            ("$observedUtc", FormatDateTime(observedUtc)));
    }

    private static async Task UpsertSectorsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long participantId,
        DriverSnapshot driver,
        DateTimeOffset observedUtc,
        CancellationToken cancellationToken)
    {
        foreach (var sector in driver.Sectors)
        {
            await ExecuteAsync(
                connection,
                """
                INSERT INTO sectors (participant_id, sector_number, best_seconds, last_seconds, current_seconds, is_overall_best, observed_utc)
                VALUES ($participantId, $sectorNumber, $bestSeconds, $lastSeconds, $currentSeconds, $isOverallBest, $observedUtc)
                ON CONFLICT(participant_id, sector_number) DO UPDATE SET
                    best_seconds = excluded.best_seconds,
                    last_seconds = excluded.last_seconds,
                    current_seconds = excluded.current_seconds,
                    is_overall_best = excluded.is_overall_best,
                    observed_utc = excluded.observed_utc;
                """,
                cancellationToken,
                transaction,
                ("$participantId", participantId),
                ("$sectorNumber", sector.Number),
                ("$bestSeconds", DbValue(sector.BestSeconds)),
                ("$lastSeconds", DbValue(sector.LastSeconds)),
                ("$currentSeconds", DbValue(sector.CurrentSeconds)),
                ("$isOverallBest", sector.IsOverallBest ? 1 : 0),
                ("$observedUtc", FormatDateTime(observedUtc)));
        }
    }

    private static Task UpsertSummaryResultAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sessionId,
        long participantId,
        DriverSnapshot driver,
        DateTimeOffset observedUtc,
        CancellationToken cancellationToken) => ExecuteAsync(
            connection,
            """
            INSERT INTO summary_results (session_id, participant_id, best_lap_seconds, completed_laps, latest_rank, latest_position, updated_utc)
            VALUES ($sessionId, $participantId, $bestLapSeconds, $completedLaps, $latestRank, $latestPosition, $updatedUtc)
            ON CONFLICT(session_id, participant_id) DO UPDATE SET
                best_lap_seconds = CASE
                    WHEN excluded.best_lap_seconds IS NULL THEN summary_results.best_lap_seconds
                    WHEN summary_results.best_lap_seconds IS NULL OR excluded.best_lap_seconds < summary_results.best_lap_seconds THEN excluded.best_lap_seconds
                    ELSE summary_results.best_lap_seconds
                END,
                completed_laps = excluded.completed_laps,
                latest_rank = excluded.latest_rank,
                latest_position = excluded.latest_position,
                updated_utc = excluded.updated_utc;
            """,
            cancellationToken,
            transaction,
            ("$sessionId", sessionId),
            ("$participantId", participantId),
            ("$bestLapSeconds", DbValue(driver.BestLapSeconds)),
            ("$completedLaps", driver.CompletedLaps),
            ("$latestRank", driver.LeaderboardRank),
            ("$latestPosition", DbValue(driver.Position)),
            ("$updatedUtc", FormatDateTime(observedUtc)));

    private static (string Name, object Value)[] ParticipantParameters(
        string sessionId,
        string driverId,
        string? driverProfileId,
        DriverSnapshot driver,
        DateTimeOffset observedUtc) =>
        [
            ("$sessionId", sessionId),
            ("$driverId", driverId),
            ("$driverProfileId", DbValue(driverProfileId)),
            ("$rigName", driver.RigName),
            ("$displayName", driver.DisplayName),
            ("$vehicleName", driver.VehicleName),
            ("$firstSeenUtc", FormatDateTime(observedUtc)),
            ("$lastSeenUtc", FormatDateTime(observedUtc)),
            ("$latestRank", driver.LeaderboardRank),
            ("$latestPosition", DbValue(driver.Position)),
            ("$completedLaps", driver.CompletedLaps),
            ("$bestLapSeconds", DbValue(driver.BestLapSeconds)),
            ("$lastLapSeconds", DbValue(driver.LastLapSeconds)),
            ("$currentLapSeconds", DbValue(driver.CurrentLapSeconds)),
            ("$gapToLeaderSeconds", DbValue(driver.GapToLeaderSeconds)),
            ("$gapToNextSeconds", DbValue(driver.GapToNextSeconds)),
            ("$lapsBehindLeader", DbValue(driver.LapsBehindLeader)),
            ("$trackPositionPercent", DbValue(driver.TrackPositionPercent)),
            ("$lapDistanceMeters", DbValue(driver.LapDistanceMeters)),
        ];

    private static async Task<long?> GetLapParticipantIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sessionId,
        long lapId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT laps.participant_id
            FROM laps
            INNER JOIN participants ON participants.participant_id = laps.participant_id
            WHERE participants.session_id = $sessionId AND laps.lap_id = $lapId;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);
        command.Parameters.AddWithValue("$lapId", lapId);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null ? null : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static async Task RefreshTrackBestRecordsForParticipantAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long participantId,
        CancellationToken cancellationToken)
    {
        if (!await HasRawLapsForParticipantAsync(connection, transaction, participantId, cancellationToken))
        {
            await UpdateDerivedTrackBestParticipantFieldsAsync(connection, transaction, participantId, cancellationToken);
            return;
        }

        await RefreshTrackBestRecordsAsync(connection, cancellationToken, transaction, participantId);
    }

    private static Task RefreshAllTrackBestRecordsAsync(SqliteConnection connection, CancellationToken cancellationToken) =>
        RefreshTrackBestRecordsAsync(connection, cancellationToken, transaction: null, participantId: null);

    private static async Task RefreshTrackBestRecordsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction,
        long? participantId)
    {
        var where = participantId is null ? string.Empty : "WHERE participant_id = $participantId";
        await ExecuteAsync(
            connection,
            $"DELETE FROM track_best_records {where};",
            cancellationToken,
            transaction,
            participantId is null ? [] : [("$participantId", participantId.Value)]);

        var participantFilter = participantId is null ? string.Empty : "AND participants.participant_id = $participantId";
        await ExecuteAsync(
            connection,
            $$"""
            INSERT INTO track_best_records (
                session_id, participant_id, lap_id, track_name, session_kind, rig_name, display_name, vehicle_name,
                lap_number, lap_time_seconds, valid_lap_flag, is_valid_lap, is_valid_timed_lap, count_for_history,
                participant_excluded, staff_invalidated, first_seen_utc, last_seen_utc, observed_utc)
            SELECT sessions.session_id,
                   participants.participant_id,
                   laps.lap_id,
                   sessions.track_name,
                   sessions.session_kind,
                   participants.rig_name,
                   coalesce(nullif(participants.display_name_override, ''), participants.display_name),
                   participants.vehicle_name,
                   laps.lap_number,
                   coalesce(laps.lap_time_seconds_override, laps.lap_time_seconds),
                   laps.valid_lap_flag,
                   laps.is_valid_lap,
                   laps.is_valid_timed_lap,
                   sessions.count_for_history,
                   participants.excluded_from_history,
                   laps.staff_invalidated,
                   sessions.first_seen_utc,
                   sessions.last_seen_utc,
                   laps.observed_utc
            FROM laps
            INNER JOIN participants ON participants.participant_id = laps.participant_id
            INNER JOIN sessions ON sessions.session_id = participants.session_id
            WHERE laps.lap_time_seconds > 0 {{participantFilter}};
            """,
            cancellationToken,
            transaction,
            participantId is null ? [] : [("$participantId", participantId.Value)]);
    }

    private static async Task<bool> HasRawLapsForParticipantAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long participantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT 1 FROM laps WHERE participant_id = $participantId LIMIT 1;";
        command.Parameters.AddWithValue("$participantId", participantId);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static Task UpdateDerivedTrackBestParticipantFieldsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long participantId,
        CancellationToken cancellationToken) => ExecuteAsync(
            connection,
            """
            UPDATE track_best_records
            SET display_name = (
                    SELECT coalesce(nullif(participants.display_name_override, ''), participants.display_name)
                    FROM participants
                    WHERE participants.participant_id = $participantId
                ),
                participant_excluded = (
                    SELECT participants.excluded_from_history
                    FROM participants
                    WHERE participants.participant_id = $participantId
                )
            WHERE participant_id = $participantId;
            """,
            cancellationToken,
            transaction,
            ("$participantId", participantId));

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<int> ExecuteNonQueryAsync(
        SqliteConnection connection,
        string commandText,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static Dictionary<string, string> NormalizeAliases(IReadOnlyDictionary<string, string> aliases)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases)
        {
            var rigName = alias.Key.Trim();
            var displayName = alias.Value.Trim();
            if (rigName.Length != 0 && displayName.Length != 0)
            {
                normalized[rigName] = displayName;
            }
        }

        return normalized;
    }

    private static List<PreparedSessionEntryRequest> NormalizePreparedSessionEntries(IReadOnlyList<PreparedSessionEntryRequest> entries)
    {
        var normalized = new Dictionary<string, PreparedSessionEntryRequest>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries)
        {
            var rigName = entry.RigName.Trim();
            var displayName = entry.DisplayName.Trim();
            if (rigName.Length == 0 || displayName.Length == 0)
            {
                continue;
            }

            normalized[rigName] = new PreparedSessionEntryRequest
            {
                RigName = rigName,
                DisplayName = displayName,
                DriverProfileId = NullIfWhiteSpace(entry.DriverProfileId),
            };
        }

        return normalized.Values.OrderBy(entry => entry.RigName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task<IReadOnlyList<HistoricalSessionLap>> GetSessionLapsAsync(
        SqliteConnection connection,
        string sessionId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT laps.lap_id,
                   laps.participant_id,
                   laps.lap_number,
                   laps.lap_time_seconds,
                   laps.lap_time_seconds_override,
                   coalesce(laps.lap_time_seconds_override, laps.lap_time_seconds) AS effective_lap_time_seconds,
                   laps.valid_lap_flag,
                   laps.is_valid_lap,
                   laps.is_valid_timed_lap,
                   laps.staff_invalidated,
                   CASE WHEN laps.is_valid_timed_lap = 1 AND laps.staff_invalidated = 0 THEN 1 ELSE 0 END AS counts_for_timing,
                   laps.correction_reason,
                   laps.corrected_utc,
                   laps.observed_utc
            FROM laps
            INNER JOIN participants ON participants.participant_id = laps.participant_id
            WHERE participants.session_id = $sessionId
            ORDER BY participants.latest_rank ASC, laps.lap_number ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId);

        var laps = new List<HistoricalSessionLap>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            laps.Add(ReadHistoricalSessionLap(reader));
        }

        return laps;
    }

    private static HistoricalSessionSummary ReadHistoricalSessionSummary(SqliteDataReader reader) => new()
    {
        SessionId = reader.GetString(0),
        Source = reader.GetString(1),
        TrackName = reader.GetString(2),
        SessionKind = ParseSessionKind(reader.GetString(3)),
        SessionPhase = ParseSessionPhase(reader.GetString(4)),
        FirstSeenUtc = ParseDateTime(reader.GetString(5)),
        LastSeenUtc = ParseDateTime(reader.GetString(6)),
        VehicleCount = reader.GetInt32(7),
        CountForHistory = reader.GetInt32(8) == 1,
        ParticipantCount = ReadInt32(reader, 9),
        LapCount = ReadInt32(reader, 10),
        ValidTimedLapCount = ReadInt32(reader, 11),
        BestLapSeconds = reader.IsDBNull(12) ? null : reader.GetDouble(12),
    };

    private static HistoricalSessionParticipant ReadHistoricalSessionParticipant(SqliteDataReader reader) => new()
    {
        ParticipantId = reader.GetInt64(0),
        Rank = reader.GetInt32(1),
        RigName = reader.GetString(2),
        DisplayName = reader.GetString(3),
        DisplayNameOverride = reader.IsDBNull(4) ? null : reader.GetString(4),
        EffectiveDisplayName = reader.GetString(5),
        DriverProfileId = reader.IsDBNull(6) ? null : reader.GetString(6),
        VehicleName = reader.GetString(7),
        FirstSeenUtc = ParseDateTime(reader.GetString(8)),
        LastSeenUtc = ParseDateTime(reader.GetString(9)),
        CompletedLaps = reader.GetInt32(10),
        BestLapSeconds = reader.IsDBNull(11) ? null : reader.GetDouble(11),
        LastLapSeconds = reader.IsDBNull(12) ? null : reader.GetDouble(12),
        LapCount = ReadInt32(reader, 13),
        ValidTimedLapCount = ReadInt32(reader, 14),
        ExcludedFromHistory = reader.GetInt32(15) == 1,
        CorrectionReason = reader.IsDBNull(16) ? null : reader.GetString(16),
        CorrectedUtc = reader.IsDBNull(17) ? null : ParseDateTime(reader.GetString(17)),
    };

    private static HistoricalSessionLap ReadHistoricalSessionLap(SqliteDataReader reader) => new()
    {
        LapId = reader.GetInt64(0),
        ParticipantId = reader.GetInt64(1),
        LapNumber = reader.GetInt32(2),
        LapSeconds = reader.GetDouble(3),
        LapSecondsOverride = reader.IsDBNull(4) ? null : reader.GetDouble(4),
        EffectiveLapSeconds = reader.GetDouble(5),
        ValidLapFlag = reader.IsDBNull(6) ? null : reader.GetInt32(6),
        IsValidLap = reader.GetInt32(7) == 1,
        IsValidTimedLap = reader.GetInt32(8) == 1,
        StaffInvalidated = reader.GetInt32(9) == 1,
        CountsForTiming = reader.GetInt32(10) == 1,
        CorrectionReason = reader.IsDBNull(11) ? null : reader.GetString(11),
        CorrectedUtc = reader.IsDBNull(12) ? null : ParseDateTime(reader.GetString(12)),
        ObservedUtc = ParseDateTime(reader.GetString(13)),
    };

    private static DriverProfile ReadDriverProfile(SqliteDataReader reader) => new()
    {
        DriverProfileId = reader.GetString(0),
        DisplayName = reader.GetString(1),
        Email = reader.IsDBNull(2) ? null : reader.GetString(2),
        Notes = reader.IsDBNull(3) ? null : reader.GetString(3),
        CreatedUtc = ParseDateTime(reader.GetString(4)),
        UpdatedUtc = ParseDateTime(reader.GetString(5)),
    };

    private static string? NullIfWhiteSpace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizeDriverId(DriverSnapshot driver) => !string.IsNullOrWhiteSpace(driver.DriverId)
        ? driver.DriverId.Trim()
        : !string.IsNullOrWhiteSpace(driver.RigName)
            ? driver.RigName.Trim()
            : $"rank-{driver.LeaderboardRank}";

    private static bool ShouldPersistSnapshot(LiveSessionSnapshot snapshot)
    {
        return snapshot.Drivers.Count > 0
            && IsKnownTrackName(snapshot.Session.TrackName)
            && snapshot.Drivers.Any(HasHistoricalLapActivity);
    }

    private static bool HasHistoricalLapActivity(DriverSnapshot driver) => driver.CompletedLaps > 0
        || IsPositiveFinite(driver.LastLapSeconds)
        || IsPositiveFinite(driver.BestLapSeconds);

    private static bool IsPositiveFinite(double? value) => value is > 0 && double.IsFinite(value.Value);

    private static bool IsKnownTrackName(string? trackName)
    {
        if (string.IsNullOrWhiteSpace(trackName))
        {
            return false;
        }

        var normalized = trackName.Trim();
        return !normalized.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
            && !normalized.Equals("Unknown track", StringComparison.OrdinalIgnoreCase)
            && !normalized.Equals("Unavailable", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSessionId(LiveSessionSnapshot snapshot, DateTimeOffset observedUtc)
    {
        var estimatedStartUtc = snapshot.Session.CurrentSessionSeconds is > 0
            ? observedUtc - TimeSpan.FromSeconds(snapshot.Session.CurrentSessionSeconds.Value)
            : observedUtc;
        var bucketTicks = estimatedStartUtc.UtcDateTime.Ticks / SessionStartBucket.Ticks * SessionStartBucket.Ticks;
        var bucketedStartUtc = new DateTimeOffset(bucketTicks, TimeSpan.Zero);
        var rawKey = string.Join(
            '|',
            snapshot.Source.Trim().ToUpperInvariant(),
            snapshot.Session.TrackName.Trim().ToUpperInvariant(),
            snapshot.Session.Kind.ToString().ToUpperInvariant(),
            snapshot.Session.ScheduledDurationSeconds?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty,
            bucketedStartUtc.UtcDateTime.ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
    }

    private static object DbValue(double? value) => value.HasValue && double.IsFinite(value.Value) ? value.Value : DBNull.Value;

    private static object DbValue(int? value) => value.HasValue ? value.Value : DBNull.Value;

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static string FormatDateTime(DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseDateTime(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static SessionKind ParseSessionKind(string value) => Enum.TryParse<SessionKind>(value, ignoreCase: true, out var kind) ? kind : SessionKind.Unknown;

    private static SessionPhase ParseSessionPhase(string value) => Enum.TryParse<SessionPhase>(value, ignoreCase: true, out var phase) ? phase : SessionPhase.Unknown;

    private static int ReadInt32(SqliteDataReader reader, int ordinal) => Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture);

    private static bool IsSameTime(double? left, double? right) => left is not null && right is not null && Math.Abs(left.Value - right.Value) < 0.0005;

    private static MonthlyTrackPeriod ReadMonthlyTrackPeriod(SqliteDataReader reader) => new()
    {
        PeriodId = reader.GetString(0),
        TrackName = reader.GetString(1),
        StartedUtc = ParseDateTime(reader.GetString(2)),
        EndedUtc = reader.IsDBNull(3) ? null : ParseDateTime(reader.GetString(3)),
        Reason = reader.IsDBNull(4) ? null : reader.GetString(4),
    };

    private static string BuildPeriodId(string trackName, DateTimeOffset startedUtc)
    {
        var rawKey = string.Join('|', trackName.Trim().ToUpperInvariant(), FormatDateTime(startedUtc));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawKey))).ToLowerInvariant();
    }

    private const string HistoricalSessionSelectSql = """
        SELECT sessions.session_id,
               sessions.source,
               sessions.track_name,
               sessions.session_kind,
               sessions.session_phase,
               sessions.first_seen_utc,
               sessions.last_seen_utc,
               sessions.vehicle_count,
               sessions.count_for_history,
               (SELECT COUNT(*)
                FROM participants
                WHERE participants.session_id = sessions.session_id) AS participant_count,
               (SELECT COUNT(*)
                FROM laps
                INNER JOIN participants lap_participants ON lap_participants.participant_id = laps.participant_id
                WHERE lap_participants.session_id = sessions.session_id) AS lap_count,
               (SELECT COUNT(*)
                FROM laps
                INNER JOIN participants timed_lap_participants ON timed_lap_participants.participant_id = laps.participant_id
                                WHERE timed_lap_participants.session_id = sessions.session_id
                                    AND timed_lap_participants.excluded_from_history = 0
                                    AND laps.is_valid_timed_lap = 1
                                    AND laps.staff_invalidated = 0) AS valid_timed_lap_count,
                             (SELECT MIN(coalesce(laps.lap_time_seconds_override, laps.lap_time_seconds))
                FROM laps
                INNER JOIN participants best_lap_participants ON best_lap_participants.participant_id = laps.participant_id
                                WHERE best_lap_participants.session_id = sessions.session_id
                                    AND best_lap_participants.excluded_from_history = 0
                                    AND laps.is_valid_timed_lap = 1
                                    AND laps.staff_invalidated = 0) AS best_lap_seconds
        FROM sessions
        """;

    private const string TrackBestRecordsSchemaSql = """
        CREATE TABLE IF NOT EXISTS track_best_records (
            track_best_record_id INTEGER PRIMARY KEY AUTOINCREMENT,
            session_id TEXT NOT NULL,
            participant_id INTEGER NOT NULL,
            lap_id INTEGER NULL,
            track_name TEXT NOT NULL,
            session_kind TEXT NOT NULL,
            rig_name TEXT NOT NULL,
            display_name TEXT NOT NULL,
            vehicle_name TEXT NOT NULL,
            lap_number INTEGER NOT NULL,
            lap_time_seconds REAL NOT NULL,
            valid_lap_flag INTEGER NULL,
            is_valid_lap INTEGER NOT NULL DEFAULT 0,
            is_valid_timed_lap INTEGER NOT NULL DEFAULT 0,
            count_for_history INTEGER NOT NULL DEFAULT 1,
            participant_excluded INTEGER NOT NULL DEFAULT 0,
            staff_invalidated INTEGER NOT NULL DEFAULT 0,
            first_seen_utc TEXT NOT NULL,
            last_seen_utc TEXT NOT NULL,
            observed_utc TEXT NOT NULL,
            UNIQUE(lap_id)
        );

        CREATE INDEX IF NOT EXISTS ix_track_best_records_board ON track_best_records(count_for_history, participant_excluded, staff_invalidated, is_valid_timed_lap, track_name, observed_utc, lap_time_seconds);
        CREATE INDEX IF NOT EXISTS ix_track_best_records_session ON track_best_records(session_id, participant_id);
        """;

    private const string SchemaSql = """
        PRAGMA journal_mode = WAL;
        PRAGMA foreign_keys = ON;

        CREATE TABLE IF NOT EXISTS schema_migrations (
            version INTEGER PRIMARY KEY,
            applied_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS store_metadata (
            key TEXT PRIMARY KEY NOT NULL,
            value TEXT NOT NULL,
            updated_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS driver_aliases (
            rig_name TEXT PRIMARY KEY NOT NULL,
            display_name TEXT NOT NULL,
            driver_profile_id TEXT NULL,
            created_utc TEXT NOT NULL,
            updated_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS driver_profiles (
            driver_profile_id TEXT PRIMARY KEY NOT NULL,
            display_name TEXT NOT NULL,
            email TEXT NULL,
            notes TEXT NULL,
            created_utc TEXT NOT NULL,
            updated_utc TEXT NOT NULL
        );

        CREATE TABLE IF NOT EXISTS monthly_track_periods (
            period_id TEXT PRIMARY KEY NOT NULL,
            track_name TEXT NOT NULL,
            started_utc TEXT NOT NULL,
            ended_utc TEXT NULL,
            reason TEXT NULL
        );

        CREATE TABLE IF NOT EXISTS sessions (
            session_id TEXT PRIMARY KEY NOT NULL,
            source TEXT NOT NULL,
            track_name TEXT NOT NULL,
            session_kind TEXT NOT NULL,
            session_phase TEXT NOT NULL,
            first_seen_utc TEXT NOT NULL,
            last_seen_utc TEXT NOT NULL,
            scheduled_duration_seconds REAL NULL,
            lap_distance_meters REAL NULL,
            vehicle_count INTEGER NOT NULL,
            air_temperature_celsius REAL NULL,
            track_temperature_celsius REAL NULL,
            rain_intensity REAL NULL,
            cloud_intensity REAL NULL,
            track_wetness REAL NULL,
            overall_flag TEXT NOT NULL,
            count_for_history INTEGER NOT NULL DEFAULT 1
        );

        CREATE TABLE IF NOT EXISTS participants (
            participant_id INTEGER PRIMARY KEY AUTOINCREMENT,
            session_id TEXT NOT NULL,
            driver_id TEXT NOT NULL,
            driver_profile_id TEXT NULL,
            rig_name TEXT NOT NULL,
            display_name TEXT NOT NULL,
            vehicle_name TEXT NOT NULL,
            first_seen_utc TEXT NOT NULL,
            last_seen_utc TEXT NOT NULL,
            latest_rank INTEGER NOT NULL,
            latest_position INTEGER NULL,
            completed_laps INTEGER NOT NULL,
            best_lap_seconds REAL NULL,
            last_lap_seconds REAL NULL,
            current_lap_seconds REAL NULL,
            gap_to_leader_seconds REAL NULL,
            gap_to_next_seconds REAL NULL,
            laps_behind_leader INTEGER NULL,
            track_position_percent REAL NULL,
            lap_distance_meters REAL NULL,
            FOREIGN KEY (session_id) REFERENCES sessions(session_id) ON DELETE CASCADE,
            UNIQUE (session_id, driver_id)
        );

        CREATE TABLE IF NOT EXISTS laps (
            lap_id INTEGER PRIMARY KEY AUTOINCREMENT,
            participant_id INTEGER NOT NULL,
            lap_number INTEGER NOT NULL,
            lap_time_seconds REAL NOT NULL,
            valid_lap_flag INTEGER NULL,
            is_valid_lap INTEGER NOT NULL DEFAULT 0,
            is_valid_timed_lap INTEGER NOT NULL DEFAULT 0,
            is_best INTEGER NOT NULL DEFAULT 0,
            observed_utc TEXT NOT NULL,
            FOREIGN KEY (participant_id) REFERENCES participants(participant_id) ON DELETE CASCADE,
            UNIQUE (participant_id, lap_number)
        );

        CREATE TABLE IF NOT EXISTS sectors (
            sector_id INTEGER PRIMARY KEY AUTOINCREMENT,
            participant_id INTEGER NOT NULL,
            sector_number INTEGER NOT NULL,
            best_seconds REAL NULL,
            last_seconds REAL NULL,
            current_seconds REAL NULL,
            is_overall_best INTEGER NOT NULL DEFAULT 0,
            observed_utc TEXT NOT NULL,
            FOREIGN KEY (participant_id) REFERENCES participants(participant_id) ON DELETE CASCADE,
            UNIQUE (participant_id, sector_number)
        );

        CREATE TABLE IF NOT EXISTS summary_results (
            summary_id INTEGER PRIMARY KEY AUTOINCREMENT,
            session_id TEXT NOT NULL,
            participant_id INTEGER NOT NULL,
            best_lap_seconds REAL NULL,
            completed_laps INTEGER NOT NULL,
            latest_rank INTEGER NOT NULL,
            latest_position INTEGER NULL,
            updated_utc TEXT NOT NULL,
            FOREIGN KEY (session_id) REFERENCES sessions(session_id) ON DELETE CASCADE,
            FOREIGN KEY (participant_id) REFERENCES participants(participant_id) ON DELETE CASCADE,
            UNIQUE (session_id, participant_id)
        );

        CREATE INDEX IF NOT EXISTS ix_sessions_history_window ON sessions(count_for_history, last_seen_utc, track_name, session_kind);
        CREATE INDEX IF NOT EXISTS ix_laps_timing ON laps(is_valid_timed_lap, lap_time_seconds, observed_utc);
        CREATE INDEX IF NOT EXISTS ix_monthly_track_periods_active ON monthly_track_periods(ended_utc, started_utc);
        """;
}

/// <summary>
/// Resolved SQLite store options.
/// </summary>
/// <param name="Enabled">True when the SQLite store should be active.</param>
/// <param name="DatabasePath">Absolute SQLite database path.</param>
public sealed record SqliteTracksideStoreOptions(bool Enabled, string DatabasePath);
