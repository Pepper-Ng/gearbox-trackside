using Trackside.Domain.LiveSession;

namespace Trackside.Application.LiveSession;

/// <summary>
/// Builds browser-facing live-session snapshots from raw leaderboard source channels.
/// </summary>
public interface ILeaderboardSnapshotBuilder
{
    /// <summary>
    /// Normalizes raw scoring channels into a sorted, highlighted kiosk snapshot.
    /// </summary>
    /// <param name="source">Raw leaderboard source channels.</param>
    /// <param name="aliases">Alias map used to resolve display names.</param>
    /// <param name="timestampUtc">Publication timestamp for the normalized snapshot.</param>
    /// <param name="updateSequence">Host-local update sequence for reconnect recovery.</param>
    /// <returns>A browser-facing live-session snapshot.</returns>
    LiveSessionSnapshot Build(
        LeaderboardSourceSnapshot source,
        DriverAliasMap aliases,
        DateTimeOffset timestampUtc,
        long updateSequence);
}

/// <summary>
/// Default leaderboard normalization, ordering, alias, and highlight rules.
/// </summary>
public sealed class LeaderboardSnapshotBuilder : ILeaderboardSnapshotBuilder
{
    /// <inheritdoc />
    public LiveSessionSnapshot Build(
        LeaderboardSourceSnapshot source,
        DriverAliasMap aliases,
        DateTimeOffset timestampUtc,
        long updateSequence)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(aliases);

        var session = new LiveSessionInfo
        {
            TrackName = string.IsNullOrWhiteSpace(source.Session.TrackName) ? "Unknown track" : source.Session.TrackName,
            Kind = source.Session.Kind,
            Phase = source.Session.Phase,
            CurrentSessionSeconds = source.Session.CurrentSessionSeconds,
            ScheduledDurationSeconds = source.Session.ScheduledDurationSeconds,
            LapDistanceMeters = source.Session.LapDistanceMeters,
            VehicleCount = source.Session.VehicleCount ?? source.Drivers.Count,
            AirTemperatureCelsius = source.Session.AirTemperatureCelsius,
            TrackTemperatureCelsius = source.Session.TrackTemperatureCelsius,
            RainIntensity = source.Session.RainIntensity,
            CloudIntensity = source.Session.CloudIntensity,
            TrackWetness = source.Session.TrackWetness,
            OverallFlag = string.IsNullOrWhiteSpace(source.Session.OverallFlag) ? "Unknown" : source.Session.OverallFlag,
        };

        var drivers = source.Drivers
            .Where(driver => !string.IsNullOrWhiteSpace(driver.RigName))
            .Select(driver => NormalizeDriver(driver, source.Session, aliases))
            .ToList();

        drivers = ApplyHighlights(drivers);
        drivers = ApplyOrdering(drivers, session.Kind);

        return new LiveSessionSnapshot
        {
            Source = source.Source,
            Status = source.Status,
            TimestampUtc = timestampUtc,
            UpdateSequence = updateSequence,
            Session = session,
            Drivers = drivers,
        };
    }

    private static DriverSnapshot NormalizeDriver(
        LeaderboardDriverSource source,
        LeaderboardSessionSource session,
        DriverAliasMap aliases)
    {
        var trackPositionPercent = source.TrackPositionPercent ?? PercentOfLap(source.LapDistanceMeters, session.LapDistanceMeters);
        var sectors = BuildSectors(source);

        return new DriverSnapshot
        {
            DriverId = string.IsNullOrWhiteSpace(source.DriverId) ? source.RigName : source.DriverId,
            RigName = source.RigName,
            DisplayName = aliases.Resolve(source.RigName),
            VehicleName = source.VehicleName,
            Position = source.RacePosition,
            CompletedLaps = source.CompletedLaps,
            ValidLapFlag = source.ValidLapFlag,
            BestLapSeconds = source.BestLapSeconds,
            LastLapSeconds = source.LastLapSeconds,
            CurrentLapSeconds = source.CurrentLapSeconds,
            GapToLeaderSeconds = source.GapToLeaderSeconds,
            GapToNextSeconds = source.GapToNextSeconds,
            LapsBehindLeader = source.LapsBehindLeader,
            CurrentSector = source.CurrentSector,
            TrackPositionPercent = trackPositionPercent,
            LapDistanceMeters = source.LapDistanceMeters,
            Sectors = sectors,
        };
    }

    private static List<SectorSnapshot> BuildSectors(LeaderboardDriverSource source)
    {
        var bestSector1 = source.BestSector1Seconds ?? source.BestLapSector1Seconds;
        var bestSector2 = SectorDelta(
            source.BestSector2CumulativeSeconds ?? source.BestLapSector2CumulativeSeconds,
            source.BestSector1Seconds ?? source.BestLapSector1Seconds);
        var bestSector3 = SectorDelta(
            source.BestLapSeconds,
            source.BestLapSector2CumulativeSeconds ?? source.BestSector2CumulativeSeconds);

        var lastSector2 = SectorDelta(source.LastSector2CumulativeSeconds, source.LastSector1Seconds);
        var lastSector3 = SectorDelta(source.LastLapSeconds, source.LastSector2CumulativeSeconds);

        var currentSector2 = SectorDelta(source.CurrentSector2CumulativeSeconds, source.CurrentSector1Seconds);

        return
        [
            new SectorSnapshot
            {
                Number = 1,
                BestSeconds = bestSector1,
                LastSeconds = source.LastSector1Seconds,
                CurrentSeconds = source.CurrentSector1Seconds,
            },
            new SectorSnapshot
            {
                Number = 2,
                BestSeconds = bestSector2,
                LastSeconds = lastSector2,
                CurrentSeconds = currentSector2,
            },
            new SectorSnapshot
            {
                Number = 3,
                BestSeconds = bestSector3,
                LastSeconds = lastSector3,
            },
        ];
    }

    private static List<DriverSnapshot> ApplyHighlights(List<DriverSnapshot> drivers)
    {
        var lapTimes = drivers
            .Select(driver => driver.BestLapSeconds)
            .Where(IsUsableTime)
            .Select(seconds => seconds!.Value)
            .ToList();
        double? bestLap = lapTimes.Count == 0 ? null : lapTimes.Min();

        var bestSectors = drivers
            .SelectMany(driver => driver.Sectors)
            .Where(sector => IsUsableTime(sector.BestSeconds))
            .GroupBy(sector => sector.Number)
            .ToDictionary(group => group.Key, group => group.Min(sector => sector.BestSeconds!.Value));

        return drivers
            .Select(driver => driver with
            {
                IsOverallBestLap = IsSameTime(driver.BestLapSeconds, bestLap),
                Sectors = driver.Sectors
                    .Select(sector => sector with
                    {
                        IsOverallBest = bestSectors.TryGetValue(sector.Number, out var bestSector)
                            && IsSameTime(sector.BestSeconds, bestSector),
                    })
                    .ToList(),
            })
            .ToList();
    }

    private static List<DriverSnapshot> ApplyOrdering(List<DriverSnapshot> drivers, SessionKind kind)
    {
        var ordered = kind switch
        {
            SessionKind.Practice or SessionKind.Qualifying => drivers
                .OrderBy(driver => driver.BestLapSeconds is null ? 1 : 0)
                .ThenBy(driver => driver.BestLapSeconds ?? double.MaxValue)
                .ThenBy(driver => driver.Position ?? int.MaxValue)
                .ThenBy(driver => driver.RigName, StringComparer.OrdinalIgnoreCase),

            SessionKind.Race => drivers
                .OrderBy(driver => driver.Position ?? int.MaxValue)
                .ThenByDescending(driver => driver.CompletedLaps)
                .ThenByDescending(driver => driver.TrackPositionPercent ?? -1.0)
                .ThenBy(driver => driver.RigName, StringComparer.OrdinalIgnoreCase),

            _ => drivers
                .OrderBy(driver => driver.Position ?? int.MaxValue)
                .ThenBy(driver => driver.RigName, StringComparer.OrdinalIgnoreCase),
        };

        return ordered
            .Select((driver, index) => driver with { LeaderboardRank = index + 1 })
            .ToList();
    }

    private static double? PercentOfLap(double? lapDistanceMeters, double? sessionLapDistanceMeters)
    {
        if (lapDistanceMeters is null || sessionLapDistanceMeters is null || sessionLapDistanceMeters <= 0)
        {
            return null;
        }

        return Math.Round(Math.Clamp(lapDistanceMeters.Value / sessionLapDistanceMeters.Value * 100.0, 0.0, 100.0), 2);
    }

    private static double? SectorDelta(double? cumulativeSeconds, double? previousCumulativeSeconds)
    {
        if (cumulativeSeconds is null || previousCumulativeSeconds is null)
        {
            return null;
        }

        var delta = cumulativeSeconds.Value - previousCumulativeSeconds.Value;
        return IsUsableTime(delta) ? Math.Round(delta, 3) : null;
    }

    private static bool IsUsableTime(double? seconds) => seconds is > 0 and < 86400 && double.IsFinite(seconds.Value);

    private static bool IsSameTime(double? left, double? right) =>
        left is not null && right is not null && Math.Abs(left.Value - right.Value) < 0.0005;
}
