using Trackside.Application.LiveSession;
using Trackside.Domain.LiveSession;

namespace Trackside.Tests;

/// <summary>
/// Covers Phase 1 leaderboard normalization rules before any live rFactor 2 source is required.
/// </summary>
public sealed class LeaderboardSnapshotBuilderTests
{
    /// <summary>
    /// Practice sessions rank drivers by fastest completed lap, not source race place.
    /// </summary>
    [Fact]
    public void PracticeOrdersByBestLap()
    {
        var snapshot = BuildSnapshot(SessionKind.Practice);

        Assert.Equal(["Setup3", "Setup1", "Setup2", "Setup4"], snapshot.Drivers.Select(driver => driver.RigName));
        Assert.Equal([1, 2, 3, 4], snapshot.Drivers.Select(driver => driver.LeaderboardRank));
    }

    /// <summary>
    /// Qualifying sessions use the same fastest-lap ordering as practice.
    /// </summary>
    [Fact]
    public void QualifyingOrdersByBestLap()
    {
        var snapshot = BuildSnapshot(SessionKind.Qualifying);

        Assert.Equal(["Setup3", "Setup1", "Setup2", "Setup4"], snapshot.Drivers.Select(driver => driver.RigName));
    }

    /// <summary>
    /// Race sessions rank drivers by current scored race position.
    /// </summary>
    [Fact]
    public void RaceOrdersByRacePosition()
    {
        var snapshot = BuildSnapshot(SessionKind.Race);

        Assert.Equal(["Setup1", "Setup2", "Setup3", "Setup4"], snapshot.Drivers.Select(driver => driver.RigName));
    }

    /// <summary>
    /// Fastest lap and sector highlights are computed from all visible rows.
    /// </summary>
    [Fact]
    public void AppliesFastestLapAndSectorHighlights()
    {
        var snapshot = BuildSnapshot(SessionKind.Practice);

        var setup3 = snapshot.Drivers.Single(driver => driver.RigName == "Setup3");
        var setup1 = snapshot.Drivers.Single(driver => driver.RigName == "Setup1");

        Assert.True(setup3.IsOverallBestLap);
        Assert.True(setup3.Sectors.Single(sector => sector.Number == 1).IsOverallBest);
        Assert.True(setup1.Sectors.Single(sector => sector.Number == 2).IsOverallBest);
        Assert.True(setup3.Sectors.Single(sector => sector.Number == 3).IsOverallBest);
    }

    /// <summary>
    /// Fixed rig names can be mapped to staff-entered display aliases without changing the source row.
    /// </summary>
    [Fact]
    public void AppliesDisplayAliases()
    {
        var snapshot = BuildSnapshot(SessionKind.Practice, new Dictionary<string, string> { ["Setup1"] = "Maya" });

        var setup1 = snapshot.Drivers.Single(driver => driver.RigName == "Setup1");
        Assert.Equal("Maya", setup1.DisplayName);
        Assert.Equal("Setup1", setup1.RigName);
    }

    /// <summary>
    /// Race distance and sector flags are retained for kiosk lap and local-yellow display.
    /// </summary>
    [Fact]
    public void CarriesLiveSessionRaceAndFlagMetadata()
    {
        var snapshot = BuildSnapshot(SessionKind.Race);

        Assert.Equal(7, snapshot.Session.TotalLaps);
        Assert.Equal(["Green", "Yellow", "Green"], snapshot.Session.SectorFlags);
    }

    private static LiveSessionSnapshot BuildSnapshot(SessionKind kind, IReadOnlyDictionary<string, string>? aliases = null)
    {
        var source = new LeaderboardSourceSnapshot
        {
            Source = "test",
            Status = "builder test",
            Session = new LeaderboardSessionSource
            {
                TrackName = "Loch Drummond - Short",
                Kind = kind,
                Phase = SessionPhase.GreenFlag,
                TotalLaps = 7,
                LapDistanceMeters = 3200.0,
                OverallFlag = "GREEN",
                SectorFlags = ["Green", "Yellow", "Green"],
            },
            Drivers =
            [
                new LeaderboardDriverSource
                {
                    DriverId = "2",
                    RigName = "Setup2",
                    VehicleName = "Formula Pro",
                    RacePosition = 2,
                    CompletedLaps = 3,
                    BestLapSeconds = 83.104,
                    LastLapSeconds = 84.12,
                    CurrentLapSeconds = 48.6,
                    BestSector1Seconds = 25.4,
                    BestSector2CumulativeSeconds = 53.8,
                    BestLapSector1Seconds = 25.4,
                    BestLapSector2CumulativeSeconds = 53.3,
                    LastSector1Seconds = 25.9,
                    LastSector2CumulativeSeconds = 54.2,
                    GapToLeaderSeconds = 0.687,
                    LapDistanceMeters = 2250.0,
                },
                new LeaderboardDriverSource
                {
                    DriverId = "1",
                    RigName = "Setup1",
                    VehicleName = "Formula Pro",
                    RacePosition = 1,
                    CompletedLaps = 3,
                    BestLapSeconds = 82.417,
                    LastLapSeconds = 83.901,
                    CurrentLapSeconds = 36.2,
                    BestSector1Seconds = 25.211,
                    BestSector2CumulativeSeconds = 53.102,
                    BestLapSector1Seconds = 25.211,
                    BestLapSector2CumulativeSeconds = 53.102,
                    LastSector1Seconds = 25.8,
                    LastSector2CumulativeSeconds = 53.7,
                    GapToLeaderSeconds = 0.0,
                    LapDistanceMeters = 1450.0,
                },
                new LeaderboardDriverSource
                {
                    DriverId = "3",
                    RigName = "Setup3",
                    VehicleName = "Formula Pro",
                    RacePosition = 3,
                    CompletedLaps = 2,
                    BestLapSeconds = 81.99,
                    LastLapSeconds = 85.5,
                    CurrentLapSeconds = 71.8,
                    BestSector1Seconds = 25.1,
                    BestSector2CumulativeSeconds = 53.0,
                    BestLapSector1Seconds = 25.1,
                    BestLapSector2CumulativeSeconds = 53.0,
                    LastSector1Seconds = 26.3,
                    LastSector2CumulativeSeconds = 55.4,
                    GapToLeaderSeconds = 2.575,
                    LapDistanceMeters = 2975.0,
                },
                new LeaderboardDriverSource
                {
                    DriverId = "4",
                    RigName = "Setup4",
                    VehicleName = "Formula Pro",
                    RacePosition = 4,
                    CompletedLaps = 0,
                    CurrentLapSeconds = 19.4,
                    LapDistanceMeters = 780.0,
                },
            ],
        };

        return new LeaderboardSnapshotBuilder().Build(
            source,
            new DriverAliasMap(aliases),
            DateTimeOffset.Parse("2026-06-24T12:00:00+00:00"),
            updateSequence: 1);
    }
}