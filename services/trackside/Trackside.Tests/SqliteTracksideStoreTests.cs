using Microsoft.Data.Sqlite;
using Trackside.Application.Configuration;
using Trackside.Application.Persistence;
using Trackside.Domain.LiveSession;
using Trackside.Infrastructure.Persistence;

namespace Trackside.Tests;

/// <summary>
/// Covers the Phase 2 persistence contract using the SQLite adapter.
/// </summary>
public sealed class SqliteTracksideStoreTests
{
    /// <summary>
    /// Aliases are persisted behind the store contract and legacy config seeding only runs once.
    /// </summary>
    [Fact]
    public async Task PersistsDriverAliasesAndSeedsLegacyConfigOnlyOnce()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);

        Assert.Equal("SQLite", store.Status.Provider);
        Assert.True(store.Status.IsEnabled);

        await store.SeedDriverAliasesAsync(
            new Dictionary<string, string> { ["Setup1"] = "Maya" },
            CancellationToken.None);

        var seededAliases = await store.GetDriverAliasesAsync(CancellationToken.None);
        Assert.Equal("Maya", seededAliases["Setup1"]);

        await store.SaveDriverAliasesAsync(
            new Dictionary<string, string> { ["Setup2"] = "Noah" },
            CancellationToken.None);
        await store.SeedDriverAliasesAsync(
            new Dictionary<string, string> { ["Setup1"] = "Maya" },
            CancellationToken.None);

        var reloadedStore = CreateStore(temporaryDirectory);
        var savedAliases = await reloadedStore.GetDriverAliasesAsync(CancellationToken.None);
        Assert.False(savedAliases.ContainsKey("Setup1"));
        Assert.Equal("Noah", savedAliases["Setup2"]);
    }

    /// <summary>
    /// Live snapshots are persisted into historical-board tables and queried through the store contract.
    /// </summary>
    [Fact]
    public async Task PersistsLiveSnapshotAndReturnsBestLaps()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        var snapshot = BuildSnapshot();

        await store.SaveLiveSessionSnapshotAsync(snapshot, countForHistory: true, CancellationToken.None);

        var reloadedStore = CreateStore(temporaryDirectory);
        var bestLaps = await reloadedStore.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
            SessionKind = SessionKind.Practice,
        }, CancellationToken.None);

        Assert.Equal(2, bestLaps.Count);
        Assert.Equal("Noah", bestLaps[0].DisplayName);
        Assert.Equal("Setup2", bestLaps[0].RigName);
        Assert.Equal(82.8, bestLaps[0].BestLapSeconds, precision: 3);
        Assert.Equal("Maya", bestLaps[1].DisplayName);
        Assert.All(bestLaps, lap => Assert.True(lap.IsValidTimedLap));
    }

    /// <summary>
    /// Staff can browse persisted sessions with participant rows and exclude a session from historical boards.
    /// </summary>
    [Fact]
    public async Task ListsSessionsWithParticipantsAndCanExcludeFromHistory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        var snapshot = BuildSnapshot();

        await store.SaveLiveSessionSnapshotAsync(snapshot, countForHistory: true, CancellationToken.None);

        var sessions = await store.GetHistoricalSessionsAsync(new HistoricalSessionQuery(), CancellationToken.None);

        var session = Assert.Single(sessions);
        Assert.True(session.CountForHistory);
        Assert.Equal("Loch Drummond - Short", session.TrackName);
        Assert.Equal(SessionKind.Practice, session.SessionKind);
        Assert.Equal(2, session.ParticipantCount);
        Assert.Equal(2, session.LapCount);
        Assert.Equal(2, session.ValidTimedLapCount);
        Assert.NotNull(session.BestLapSeconds);
        Assert.Equal(82.8, session.BestLapSeconds.Value, precision: 3);

        var detail = await store.GetHistoricalSessionAsync(session.SessionId, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(2, detail.Participants.Count);
        Assert.Contains(detail.Participants, participant => participant.DisplayName == "Maya" && participant.ValidTimedLapCount == 1);
        Assert.Contains(detail.Participants, participant => participant.DisplayName == "Noah" && participant.CompletedLaps == 5);

        Assert.True(await store.SetSessionCountForHistoryAsync(session.SessionId, countForHistory: false, CancellationToken.None));
        var excludedDetail = await store.GetHistoricalSessionAsync(session.SessionId, CancellationToken.None);
        var bestLaps = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
        }, CancellationToken.None);

        Assert.NotNull(excludedDetail);
        Assert.False(excludedDetail.CountForHistory);
        Assert.Empty(bestLaps);

        var laterSnapshot = snapshot with
        {
            TimestampUtc = snapshot.TimestampUtc.AddMinutes(1),
            Session = snapshot.Session with { CurrentSessionSeconds = snapshot.Session.CurrentSessionSeconds + 60.0 },
        };
        await store.SaveLiveSessionSnapshotAsync(laterSnapshot, countForHistory: true, CancellationToken.None);

        var stillExcludedDetail = await store.GetHistoricalSessionAsync(session.SessionId, CancellationToken.None);

        Assert.NotNull(stillExcludedDetail);
        Assert.False(stillExcludedDetail.CountForHistory);
    }

    /// <summary>
    /// Participant corrections and lap invalidations flow through to historical best-lap boards.
    /// </summary>
    [Fact]
    public async Task ParticipantAndLapCorrectionsUpdateHistoricalBoards()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        await store.SaveLiveSessionSnapshotAsync(BuildSnapshot(), countForHistory: true, CancellationToken.None);
        var session = Assert.Single(await store.GetHistoricalSessionsAsync(new HistoricalSessionQuery(), CancellationToken.None));
        var detail = await store.GetHistoricalSessionAsync(session.SessionId, CancellationToken.None);
        Assert.NotNull(detail);
        var noah = detail.Participants.Single(participant => participant.DisplayName == "Noah");
        var maya = detail.Participants.Single(participant => participant.DisplayName == "Maya");

        detail = await store.CorrectLapAsync(
            session.SessionId,
            noah.Laps.Single().LapId,
            new LapCorrectionRequest { StaffInvalidated = true, Reason = "Timing marshal invalidated lap" },
            CancellationToken.None);
        detail = await store.CorrectParticipantAsync(
            session.SessionId,
            maya.ParticipantId,
            new ParticipantCorrectionRequest { DisplayNameOverride = "Maya Corrected", Reason = "Name spelling" },
            CancellationToken.None);

        Assert.NotNull(detail);
        var correctedMaya = detail.Participants.Single(participant => participant.ParticipantId == maya.ParticipantId);
        Assert.Equal("Maya", correctedMaya.DisplayName);
        Assert.Equal("Maya Corrected", correctedMaya.EffectiveDisplayName);
        Assert.Equal("Maya Corrected", correctedMaya.DisplayNameOverride);

        var bestLaps = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
        }, CancellationToken.None);

        Assert.Single(bestLaps);
        Assert.Equal("Maya Corrected", bestLaps[0].DisplayName);
        Assert.Equal(83.4, bestLaps[0].BestLapSeconds, precision: 3);

        await store.CorrectParticipantAsync(
            session.SessionId,
            maya.ParticipantId,
            new ParticipantCorrectionRequest { DisplayNameOverride = "Maya Corrected", ExcludedFromHistory = true, Reason = "Wrong driver" },
            CancellationToken.None);

        Assert.Empty(await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
        }, CancellationToken.None));
    }

    /// <summary>
    /// Retention can prune raw laps after derived track-best records exist without breaking historical boards.
    /// </summary>
    [Fact]
    public async Task RetentionPrunesRawLapsButPreservesDerivedBoards()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        await store.SaveLiveSessionSnapshotAsync(BuildSnapshot(), countForHistory: true, CancellationToken.None);

        var result = await store.EnforceRetentionAsync(
            new TracksideRetentionOptions { DetailedLapRecordsDays = 1, SessionSummariesDays = 730 },
            DateTimeOffset.Parse("2026-07-01T00:00:00+00:00"),
            CancellationToken.None);
        var bestLaps = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
        }, CancellationToken.None);

        Assert.Equal(2, result.DetailedLapRecordsDeleted);
        Assert.Equal(2, bestLaps.Count);

        var session = Assert.Single(await store.GetHistoricalSessionsAsync(new HistoricalSessionQuery(), CancellationToken.None));
        var detail = await store.GetHistoricalSessionAsync(session.SessionId, CancellationToken.None);
        Assert.NotNull(detail);
        var maya = detail.Participants.Single(participant => participant.DisplayName == "Maya");

        await store.CorrectParticipantAsync(
            session.SessionId,
            maya.ParticipantId,
            new ParticipantCorrectionRequest { DisplayNameOverride = "Maya After Retention", Reason = "Correction after raw lap pruning" },
            CancellationToken.None);

        bestLaps = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
        }, CancellationToken.None);

        Assert.Contains(bestLaps, lap => lap.DisplayName == "Maya After Retention");

        result = await store.EnforceRetentionAsync(
            new TracksideRetentionOptions { DetailedLapRecordsDays = 1, SessionSummariesDays = 730, TrackBestRecordsDays = 1 },
            DateTimeOffset.Parse("2026-07-01T00:00:00+00:00"),
            CancellationToken.None);

        Assert.Equal(2, result.TrackBestRecordsDeleted);
        Assert.Empty(await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
        }, CancellationToken.None));
    }

    /// <summary>
    /// Session summary retention deletes child rows through SQLite foreign-key cascades.
    /// </summary>
    [Fact]
    public async Task SessionRetentionCascadesChildRows()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        await store.SaveLiveSessionSnapshotAsync(BuildSnapshot(), countForHistory: true, CancellationToken.None);

        var result = await store.EnforceRetentionAsync(
            new TracksideRetentionOptions { DetailedLapRecordsDays = 35, SessionSummariesDays = 1 },
            DateTimeOffset.Parse("2026-07-01T00:00:00+00:00"),
            CancellationToken.None);

        Assert.Equal(1, result.SessionSummariesDeleted);
        Assert.Empty(await store.GetHistoricalSessionsAsync(new HistoricalSessionQuery(), CancellationToken.None));

        var databasePath = Path.Combine(temporaryDirectory.Path, "trackside-test.db");
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString());
        await connection.OpenAsync();

        Assert.Equal(0L, await CountRowsAsync(connection, "participants"));
        Assert.Equal(0L, await CountRowsAsync(connection, "laps"));
        Assert.Equal(0L, await CountRowsAsync(connection, "sectors"));
        Assert.Equal(0L, await CountRowsAsync(connection, "summary_results"));
    }

    /// <summary>
    /// Laps that rFactor 2 marks as not counting for time are stored but excluded from best-lap boards.
    /// </summary>
    [Fact]
    public async Task ExcludesLapsThatDoNotCountForTiming()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        var snapshot = BuildSnapshot() with
        {
            Drivers =
            [
                BuildSnapshot().Drivers[0] with { ValidLapFlag = 1, LastLapSeconds = 70.0, BestLapSeconds = 70.0 },
                BuildSnapshot().Drivers[1],
            ],
        };

        await store.SaveLiveSessionSnapshotAsync(snapshot, countForHistory: true, CancellationToken.None);

        var bestLaps = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
        }, CancellationToken.None);

        Assert.Single(bestLaps);
        Assert.Equal("Noah", bestLaps[0].DisplayName);
        Assert.Equal(2, bestLaps[0].ValidLapFlag);
    }

    /// <summary>
    /// Public board mode defaults to one fastest lap per driver while all-laps mode can show repeated driver laps.
    /// </summary>
    [Fact]
    public async Task SupportsPerDriverAndAllLapsModes()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        var firstSnapshot = BuildSnapshot() with
        {
            Drivers = [BuildSnapshot().Drivers[0]],
        };
        var secondSnapshot = firstSnapshot with
        {
            TimestampUtc = firstSnapshot.TimestampUtc.AddMinutes(3),
            Drivers = [firstSnapshot.Drivers[0] with { CompletedLaps = 5, LastLapSeconds = 81.0, BestLapSeconds = 81.0 }],
        };

        await store.SaveLiveSessionSnapshotAsync(firstSnapshot, countForHistory: true, CancellationToken.None);
        await store.SaveLiveSessionSnapshotAsync(secondSnapshot, countForHistory: true, CancellationToken.None);

        var perDriver = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
            Mode = BestLapBoardMode.PerDriver,
        }, CancellationToken.None);
        var allLaps = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
            Mode = BestLapBoardMode.AllLaps,
        }, CancellationToken.None);

        Assert.Single(perDriver);
        Assert.Equal(81.0, perDriver[0].BestLapSeconds, precision: 3);
        Assert.Equal(2, allLaps.Count);
    }

    /// <summary>
    /// Updating a driver's display name during a running session updates the same participant row,
    /// so earlier laps resolve through the latest session participant name.
    /// </summary>
    [Fact]
    public async Task UpdatingAliasDuringSessionKeepsLapsOnSameParticipant()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        var firstSnapshot = BuildSnapshot() with
        {
            Drivers = [BuildSnapshot().Drivers[0]],
        };
        var renamedSnapshot = firstSnapshot with
        {
            TimestampUtc = firstSnapshot.TimestampUtc.AddMinutes(1),
            Session = firstSnapshot.Session with { CurrentSessionSeconds = firstSnapshot.Session.CurrentSessionSeconds + 60.0 },
            Drivers =
            [
                firstSnapshot.Drivers[0] with
                {
                    DisplayName = "Maya Corrected",
                    CompletedLaps = 5,
                    LastLapSeconds = 81.0,
                    BestLapSeconds = 81.0,
                },
            ],
        };

        await store.SaveLiveSessionSnapshotAsync(firstSnapshot, countForHistory: true, CancellationToken.None);
        await store.SaveLiveSessionSnapshotAsync(renamedSnapshot, countForHistory: true, CancellationToken.None);

        var allLaps = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            TrackName = "Loch Drummond - Short",
            Mode = BestLapBoardMode.AllLaps,
        }, CancellationToken.None);

        Assert.Equal(2, allLaps.Count);
        Assert.All(allLaps, lap => Assert.Equal("Maya Corrected", lap.DisplayName));
        Assert.Equal([5, 4], allLaps.Select(lap => lap.LapNumber));
    }

    /// <summary>
    /// Starting or resetting the monthly track creates a fresh active period without deleting old records.
    /// </summary>
    [Fact]
    public async Task StartsFreshMonthlyTrackPeriod()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        var first = await store.StartMonthlyTrackAsync(
            "Loch Drummond - Short",
            DateTimeOffset.Parse("2026-06-01T10:00:00+00:00"),
            "June challenge",
            CancellationToken.None);
        var second = await store.StartMonthlyTrackAsync(
            "Silverstone GP",
            DateTimeOffset.Parse("2026-07-01T10:00:00+00:00"),
            "July challenge",
            CancellationToken.None);

        var active = await store.GetActiveMonthlyTrackAsync(CancellationToken.None);

        Assert.NotEqual(first.PeriodId, second.PeriodId);
        Assert.NotNull(active);
        Assert.Equal("Silverstone GP", active.TrackName);
        Assert.Equal("July challenge", active.Reason);
    }

    /// <summary>
    /// Prepared session setup can optionally link a rig assignment to a recurring-customer profile and can be cleared.
    /// </summary>
    [Fact]
    public async Task SavesPreparedSessionSetupWithOptionalDriverProfile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        ITracksideStore store = CreateStore(temporaryDirectory);
        var profile = await store.CreateDriverProfileAsync(new DriverProfileRequest
        {
            DisplayName = "Maya Profile",
            Email = "maya@example.invalid",
        }, CancellationToken.None);

        await store.SavePreparedSessionEntriesAsync(
        [
            new PreparedSessionEntryRequest
            {
                RigName = "Setup1",
                DisplayName = "Maya",
                DriverProfileId = profile.DriverProfileId,
            },
        ], CancellationToken.None);

        var entries = await store.GetPreparedSessionEntriesAsync(CancellationToken.None);
        Assert.Single(entries);
        Assert.Equal("Setup1", entries[0].RigName);
        Assert.Equal("Maya", entries[0].DisplayName);
        Assert.Equal(profile.DriverProfileId, entries[0].DriverProfileId);
        Assert.Equal("Maya Profile", entries[0].DriverProfileDisplayName);

        await store.ClearPreparedSessionEntriesAsync(CancellationToken.None);

        Assert.Empty(await store.GetPreparedSessionEntriesAsync(CancellationToken.None));
    }

    private static ITracksideStore CreateStore(TemporaryDirectory temporaryDirectory)
    {
        var databasePath = Path.Combine(temporaryDirectory.Path, "trackside-test.db");
        return new SqliteTracksideStore(new SqliteTracksideStoreOptions(true, databasePath), TimeProvider.System);
    }

    private static LiveSessionSnapshot BuildSnapshot() => new()
    {
        Source = "fixture",
        Status = "fixture test",
        TimestampUtc = DateTimeOffset.Parse("2026-06-25T12:15:30+00:00"),
        UpdateSequence = 10,
        Session = new LiveSessionInfo
        {
            TrackName = "Loch Drummond - Short",
            Kind = SessionKind.Practice,
            Phase = SessionPhase.GreenFlag,
            CurrentSessionSeconds = 210.0,
            ScheduledDurationSeconds = 1800.0,
            LapDistanceMeters = 3200.0,
            VehicleCount = 2,
            AirTemperatureCelsius = 22.0,
            TrackTemperatureCelsius = 30.0,
            OverallFlag = "GREEN",
        },
        Drivers =
        [
            new DriverSnapshot
            {
                LeaderboardRank = 2,
                DriverId = "1",
                RigName = "Setup1",
                DisplayName = "Maya",
                VehicleName = "Formula Pro",
                Position = 1,
                CompletedLaps = 4,
                ValidLapFlag = 2,
                BestLapSeconds = 82.1,
                LastLapSeconds = 83.4,
                CurrentLapSeconds = 12.3,
                Sectors =
                [
                    new SectorSnapshot { Number = 1, BestSeconds = 25.1, LastSeconds = 25.4, IsOverallBest = true },
                    new SectorSnapshot { Number = 2, BestSeconds = 27.0, LastSeconds = 27.2 },
                ],
            },
            new DriverSnapshot
            {
                LeaderboardRank = 1,
                DriverId = "2",
                RigName = "Setup2",
                DisplayName = "Noah",
                VehicleName = "Formula Pro",
                Position = 2,
                CompletedLaps = 5,
                ValidLapFlag = 2,
                BestLapSeconds = 81.5,
                LastLapSeconds = 82.8,
                CurrentLapSeconds = 10.2,
                Sectors =
                [
                    new SectorSnapshot { Number = 1, BestSeconds = 25.0, LastSeconds = 25.2 },
                    new SectorSnapshot { Number = 2, BestSeconds = 26.8, LastSeconds = 26.9, IsOverallBest = true },
                ],
            },
        ],
    };

    private static async Task<long> CountRowsAsync(SqliteConnection connection, string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trackside-store-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}