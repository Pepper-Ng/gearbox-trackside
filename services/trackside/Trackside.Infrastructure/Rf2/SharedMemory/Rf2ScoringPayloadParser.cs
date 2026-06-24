using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Trackside.Application.LiveSession;
using Trackside.Domain.LiveSession;

namespace Trackside.Infrastructure.Rf2.SharedMemory;

/// <summary>
/// Contract for future rFactor 2 scoring payload parsers.
/// </summary>
public interface IRf2ScoringPayloadParser
{
    /// <summary>
    /// Expected byte size of the rF2 scoring payload structure.
    /// </summary>
    int PayloadSize { get; }

    /// <summary>
    /// Scores a candidate payload for mapped-buffer offset detection.
    /// </summary>
    /// <param name="payload">Candidate scoring payload.</param>
    /// <returns>A higher score for more plausible scoring data.</returns>
    int ScorePayload(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Converts a scoring payload into the raw Trackside leaderboard source contract.
    /// </summary>
    /// <param name="payload">Scoring payload bytes with any mapped-buffer wrapper already removed.</param>
    /// <param name="source">Logical source name.</param>
    /// <param name="status">Human-readable source status.</param>
    /// <returns>Raw leaderboard channels needed by the normalized builder.</returns>
    LeaderboardSourceSnapshot ParseSource(ReadOnlySpan<byte> payload, string source, string status);

    /// <summary>
    /// Returns true when rFactor 2's scoring update counters indicate a stable frame.
    /// </summary>
    /// <param name="payload">Candidate scoring payload.</param>
    /// <returns>True when begin and end update counters match.</returns>
    bool IsStablePayload(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Converts a scoring payload into the normalized Trackside live-session model.
    /// </summary>
    /// <param name="payload">Scoring payload bytes with any mapped-buffer wrapper already removed.</param>
    /// <param name="timestampUtc">Timestamp assigned to the normalized snapshot.</param>
    /// <param name="updateSequence">Host-local update sequence assigned to the snapshot.</param>
    /// <returns>A normalized live-session snapshot.</returns>
    LiveSessionSnapshot Parse(ReadOnlySpan<byte> payload, DateTimeOffset timestampUtc, long updateSequence);
}

/// <summary>
/// rFactor 2 scoring payload parser based on the fields proven in the Python PoC.
/// </summary>
public sealed class Rf2ScoringPayloadParser : IRf2ScoringPayloadParser
{
    private const int MaxMappedVehicles = 128;

    /// <inheritdoc />
    public int PayloadSize { get; } = Marshal.SizeOf<Rf2ScoringRaw>();

    /// <inheritdoc />
    public int ScorePayload(ReadOnlySpan<byte> payload)
    {
        try
        {
            var scoring = ReadPayload(payload);
            var info = scoring.ScoringInfo;
            var score = 0;

            var trackName = CString(info.TrackName);
            if (IsPlausibleText(trackName))
            {
                score += 10;
            }

            if (info.NumVehicles is >= 0 and <= MaxMappedVehicles)
            {
                score += 10;
            }

            if (info.Session is >= 0 and <= 13)
            {
                score += 5;
            }

            if (info.GamePhase <= 9)
            {
                score += 5;
            }

            var scanLimit = GetScanLimit(info.NumVehicles);
            var validDrivers = 0;
            for (var index = 0; index < scanLimit; index++)
            {
                if (IsProbableScoringVehicle(scoring.Vehicles[index]))
                {
                    validDrivers++;
                }
            }

            return score + Math.Min(validDrivers, 16) * 2;
        }
        catch
        {
            return 0;
        }
    }

    /// <inheritdoc />
    public LeaderboardSourceSnapshot ParseSource(ReadOnlySpan<byte> payload, string source, string status)
    {
        var scoring = ReadPayload(payload);
        var info = scoring.ScoringInfo;
        var sessionLapDistance = NonNegative(info.LapDist);
        var sectorFlags = info.SectorFlag ?? [];
        var scanLimit = GetScanLimit(info.NumVehicles);
        var drivers = new List<LeaderboardDriverSource>(scanLimit);

        for (var index = 0; index < scanLimit; index++)
        {
            var driver = ToDriverSource(scoring.Vehicles[index], sessionLapDistance);
            if (driver is not null)
            {
                drivers.Add(driver);
            }
        }

        return new LeaderboardSourceSnapshot
        {
            Source = source,
            Status = status,
            Session = new LeaderboardSessionSource
            {
                TrackName = CString(info.TrackName),
                Kind = SessionKindFromCode(info.Session),
                Phase = SessionPhaseFromGamePhase(info.GamePhase),
                CurrentSessionSeconds = NonNegative(info.CurrentET),
                ScheduledDurationSeconds = NonNegative(info.EndET),
                LapDistanceMeters = sessionLapDistance,
                AirTemperatureCelsius = ReasonableTemperature(info.AmbientTemp),
                TrackTemperatureCelsius = ReasonableTemperature(info.TrackTemp),
                RainIntensity = Clamp01(info.Raining),
                CloudIntensity = Clamp01(info.DarkCloud),
                TrackWetness = Clamp01(info.AvgPathWetness),
                OverallFlag = OverallFlagName(info.GamePhase, info.YellowFlagState, sectorFlags),
            },
            Drivers = drivers,
        };
    }

    /// <inheritdoc />
    public bool IsStablePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < sizeof(uint) * 2)
        {
            return false;
        }

        var versionBegin = BinaryPrimitives.ReadUInt32LittleEndian(payload[..sizeof(uint)]);
        var versionEnd = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(sizeof(uint), sizeof(uint)));
        return versionBegin == versionEnd;
    }

    /// <inheritdoc />
    public LiveSessionSnapshot Parse(ReadOnlySpan<byte> payload, DateTimeOffset timestampUtc, long updateSequence)
    {
        var builder = new LeaderboardSnapshotBuilder();
        var source = ParseSource(payload, "shared-memory", "connected");
        return builder.Build(source, DriverAliasMap.Empty, timestampUtc, updateSequence);
    }

    private static Rf2ScoringRaw ReadPayload(ReadOnlySpan<byte> payload)
    {
        var payloadSize = Marshal.SizeOf<Rf2ScoringRaw>();
        if (payload.Length < payloadSize)
        {
            throw new InvalidDataException($"Scoring payload is {payload.Length} bytes; expected at least {payloadSize} bytes.");
        }

        var buffer = payload[..payloadSize].ToArray();
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<Rf2ScoringRaw>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    private static LeaderboardDriverSource? ToDriverSource(Rf2VehicleScoringRaw vehicle, double? sessionLapDistance)
    {
        if (!IsProbableScoringVehicle(vehicle))
        {
            return null;
        }

        var lapDistance = NonNegative(vehicle.LapDist);
        var place = vehicle.Place == 0 ? null : (int?)vehicle.Place;

        return new LeaderboardDriverSource
        {
            DriverId = vehicle.ID.ToString(CultureInfo.InvariantCulture),
            RigName = CString(vehicle.DriverName),
            VehicleName = CString(vehicle.VehicleName),
            RacePosition = place,
            CompletedLaps = Math.Max(0, (int)vehicle.TotalLaps),
            BestLapSeconds = Timed(vehicle.BestLapTime),
            LastLapSeconds = Timed(vehicle.LastLapTime),
            CurrentLapSeconds = Timed(vehicle.TimeIntoLap),
            CurrentSector = vehicle.Sector,
            BestSector1Seconds = Timed(vehicle.BestSector1),
            BestSector2CumulativeSeconds = Timed(vehicle.BestSector2),
            BestLapSector1Seconds = Timed(vehicle.BestLapSector1),
            BestLapSector2CumulativeSeconds = Timed(vehicle.BestLapSector2),
            LastSector1Seconds = Timed(vehicle.LastSector1),
            LastSector2CumulativeSeconds = Timed(vehicle.LastSector2),
            CurrentSector1Seconds = Timed(vehicle.CurrentSector1),
            CurrentSector2CumulativeSeconds = Timed(vehicle.CurrentSector2),
            GapToNextSeconds = Timed(vehicle.TimeBehindNext),
            GapToLeaderSeconds = Timed(vehicle.TimeBehindLeader),
            LapsBehindLeader = vehicle.LapsBehindLeader < 0 ? null : vehicle.LapsBehindLeader,
            LapDistanceMeters = lapDistance,
            TrackPositionPercent = PercentOfLap(lapDistance, sessionLapDistance),
        };
    }

    private static bool IsProbableScoringVehicle(Rf2VehicleScoringRaw vehicle)
    {
        var driverName = CString(vehicle.DriverName);
        if (!IsPlausibleText(driverName))
        {
            return false;
        }

        if (vehicle.Place > MaxMappedVehicles)
        {
            return false;
        }

        if (vehicle.TotalLaps is < 0 or > 10000)
        {
            return false;
        }

        return vehicle.Sector is >= 0 and <= 2;
    }

    private static int GetScanLimit(int rawVehicleCount) =>
        rawVehicleCount is >= 0 and <= MaxMappedVehicles ? rawVehicleCount : MaxMappedVehicles;

    private static SessionKind SessionKindFromCode(int sessionCode)
    {
        if (sessionCode is >= 0 and <= 4)
        {
            return SessionKind.Practice;
        }

        if (sessionCode is >= 5 and <= 8)
        {
            return SessionKind.Qualifying;
        }

        if (sessionCode is >= 10 and <= 13)
        {
            return SessionKind.Race;
        }

        return SessionKind.Unknown;
    }

    private static SessionPhase SessionPhaseFromGamePhase(byte gamePhase) => gamePhase switch
    {
        0 => SessionPhase.Garage,
        5 or 6 => SessionPhase.GreenFlag,
        8 => SessionPhase.SessionOver,
        _ => SessionPhase.Unknown,
    };

    private static string OverallFlagName(byte gamePhase, sbyte yellowFlagState, IReadOnlyList<sbyte> sectorFlags)
    {
        if (gamePhase == 8)
        {
            return "SESSION OVER";
        }

        if (gamePhase == 7 || yellowFlagState == 7)
        {
            return "RED / RACE HALT";
        }

        if (gamePhase == 6)
        {
            return "SAFETY CAR / FULL COURSE YELLOW";
        }

        if (sectorFlags.Any(value => value == 1))
        {
            return "LOCAL YELLOW";
        }

        if (yellowFlagState is not (-1 or 0))
        {
            return "YELLOW";
        }

        return gamePhase == 5 ? "GREEN" : GamePhaseName(gamePhase).ToUpperInvariant();
    }

    private static string GamePhaseName(byte gamePhase) => gamePhase switch
    {
        0 => "Garage",
        1 => "Reconnaissance",
        2 => "Grid walk",
        3 => "Formation",
        4 => "Countdown",
        5 => "Green flag",
        6 => "Full course yellow",
        7 => "Stopped",
        8 => "Session over",
        9 => "Paused/heartbeat",
        _ => $"Unknown ({gamePhase})",
    };

    private static double? PercentOfLap(double? lapDistance, double? sessionLapDistance)
    {
        if (lapDistance is null || sessionLapDistance is null || sessionLapDistance <= 0)
        {
            return null;
        }

        return Math.Round(Math.Clamp(lapDistance.Value / sessionLapDistance.Value * 100.0, 0.0, 100.0), 2);
    }

    private static double? Timed(double value) => value is > 0 and < 86400 && double.IsFinite(value) ? Math.Round(value, 3) : null;

    private static double? NonNegative(double value) => value >= 0 && double.IsFinite(value) ? Math.Round(value, 3) : null;

    private static double? ReasonableTemperature(double value) => value is >= -100 and <= 150 && double.IsFinite(value) ? Math.Round(value, 2) : null;

    private static double? Clamp01(double value) => value is >= 0 and <= 1 && double.IsFinite(value) ? Math.Round(value, 3) : null;

    private static bool IsPlausibleText(string value) => !string.IsNullOrWhiteSpace(value) && value.All(character => !char.IsControl(character));

    private static string CString(byte[]? value)
    {
        if (value is null || value.Length == 0)
        {
            return string.Empty;
        }

        var length = Array.IndexOf(value, (byte)0);
        if (length < 0)
        {
            length = value.Length;
        }

        var raw = value.AsSpan(0, length).ToArray();
        var text = Decode(raw, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true));
        return text ?? Encoding.Latin1.GetString(raw).Trim();
    }

    private static string? Decode(byte[] value, Encoding encoding)
    {
        try
        {
            return encoding.GetString(value).Trim();
        }
        catch (DecoderFallbackException)
        {
            return null;
        }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct Rf2Vec3Raw
{
    public double X;
    public double Y;
    public double Z;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct Rf2ScoringInfoRaw
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] TrackName;
    public int Session;
    public double CurrentET;
    public double EndET;
    public int MaxLaps;
    public double LapDist;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Pointer1;
    public int NumVehicles;
    public byte GamePhase;
    public sbyte YellowFlagState;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public sbyte[] SectorFlag;
    public byte StartLight;
    public byte NumRedLights;
    public byte InRealtime;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] PlayerName;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] PlrFileName;
    public double DarkCloud;
    public double Raining;
    public double AmbientTemp;
    public double TrackTemp;
    public Rf2Vec3Raw Wind;
    public double MinPathWetness;
    public double MaxPathWetness;
    public byte GameMode;
    public byte IsPasswordProtected;
    public ushort ServerPort;
    public uint ServerPublicIP;
    public int MaxPlayers;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] ServerName;
    public float StartET;
    public double AvgPathWetness;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 200)]
    public byte[] Expansion;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] Pointer2;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct Rf2VehicleScoringRaw
{
    public int ID;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] DriverName;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] VehicleName;
    public short TotalLaps;
    public sbyte Sector;
    public sbyte FinishStatus;
    public double LapDist;
    public double PathLateral;
    public double TrackEdge;
    public double BestSector1;
    public double BestSector2;
    public double BestLapTime;
    public double LastSector1;
    public double LastSector2;
    public double LastLapTime;
    public double CurrentSector1;
    public double CurrentSector2;
    public short NumPitstops;
    public short NumPenalties;
    public byte IsPlayer;
    public sbyte Control;
    public byte InPits;
    public byte Place;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] VehicleClass;
    public double TimeBehindNext;
    public int LapsBehindNext;
    public double TimeBehindLeader;
    public int LapsBehindLeader;
    public double LapStartET;
    public Rf2Vec3Raw Pos;
    public Rf2Vec3Raw LocalVel;
    public Rf2Vec3Raw LocalAccel;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public Rf2Vec3Raw[] Ori;
    public Rf2Vec3Raw LocalRot;
    public Rf2Vec3Raw LocalRotAccel;
    public byte Headlights;
    public byte PitState;
    public byte ServerScored;
    public byte IndividualPhase;
    public int Qualification;
    public double TimeIntoLap;
    public double EstimatedLapTime;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
    public byte[] PitGroup;
    public byte Flag;
    public byte UnderYellow;
    public byte CountLapFlag;
    public byte InGarageStall;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] UpgradePack;
    public float PitLapDist;
    public float BestLapSector1;
    public float BestLapSector2;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
    public byte[] Expansion;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct Rf2ScoringRaw
{
    public uint VersionUpdateBegin;
    public uint VersionUpdateEnd;
    public int BytesUpdatedHint;
    public Rf2ScoringInfoRaw ScoringInfo;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
    public Rf2VehicleScoringRaw[] Vehicles;
}