using System.Buffers.Binary;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Trackside.Application.LiveSession;

namespace Trackside.Infrastructure.Rf2.SharedMemory;

/// <summary>
/// Contract for rFactor 2 telemetry payload parsers.
/// </summary>
public interface IRf2TelemetryPayloadParser
{
    /// <summary>
    /// Expected byte size of the rF2 telemetry payload structure.
    /// </summary>
    int PayloadSize { get; }

    /// <summary>
    /// Scores a candidate payload for mapped-buffer offset detection.
    /// </summary>
    int ScorePayload(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Returns true when telemetry update counters indicate a stable frame.
    /// </summary>
    bool IsStablePayload(ReadOnlySpan<byte> payload);

    /// <summary>
    /// Converts telemetry payload bytes into high-rate world-position samples.
    /// </summary>
    TelemetryPositionFrame ParsePositionFrame(ReadOnlySpan<byte> payload, string source);
}

/// <summary>
/// rFactor 2 telemetry payload parser used by the driver-tracker geometry recorder.
/// </summary>
public sealed class Rf2TelemetryPayloadParser : IRf2TelemetryPayloadParser
{
    private const int MaxMappedVehicles = 128;

    /// <inheritdoc />
    public int PayloadSize { get; } = Marshal.SizeOf<Rf2TelemetryRaw>();

    /// <inheritdoc />
    public int ScorePayload(ReadOnlySpan<byte> payload)
    {
        try
        {
            var telemetry = ReadPayload(payload);
            var score = telemetry.NumVehicles is >= 0 and <= MaxMappedVehicles ? 10 : 0;
            var scanLimit = Math.Clamp(telemetry.NumVehicles, 0, MaxMappedVehicles);
            for (var index = 0; index < scanLimit; index++)
            {
                var vehicle = telemetry.Vehicles[index];
                if (vehicle.ID > 0 && IsPlausibleText(CString(vehicle.TrackName)) && IsFinite(vehicle.Pos.X) && IsFinite(vehicle.Pos.Z))
                {
                    score += 2;
                }
            }

            return score;
        }
        catch
        {
            return 0;
        }
    }

    /// <inheritdoc />
    public bool IsStablePayload(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 8)
        {
            return false;
        }

        var begin = BinaryPrimitives.ReadUInt32LittleEndian(payload[..4]);
        var end = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(4, 4));
        return begin == end;
    }

    /// <inheritdoc />
    public TelemetryPositionFrame ParsePositionFrame(ReadOnlySpan<byte> payload, string source)
    {
        var telemetry = ReadPayload(payload);
        var scanLimit = Math.Clamp(telemetry.NumVehicles, 0, MaxMappedVehicles);
        var vehicles = new List<TelemetryPositionVehicle>(scanLimit);
        var trackName = string.Empty;

        for (var index = 0; index < scanLimit; index++)
        {
            var vehicle = telemetry.Vehicles[index];
            var candidateTrackName = CString(vehicle.TrackName);
            if (string.IsNullOrWhiteSpace(trackName) && IsPlausibleText(candidateTrackName))
            {
                trackName = candidateTrackName;
            }

            if (vehicle.ID <= 0 || !IsFinite(vehicle.Pos.X) || !IsFinite(vehicle.Pos.Z))
            {
                continue;
            }

            vehicles.Add(new TelemetryPositionVehicle
            {
                DriverId = vehicle.ID.ToString(CultureInfo.InvariantCulture),
                PosX = vehicle.Pos.X,
                PosY = vehicle.Pos.Y,
                PosZ = vehicle.Pos.Z,
            });
        }

        return new TelemetryPositionFrame
        {
            TrackName = string.IsNullOrWhiteSpace(trackName) ? "Unknown track" : trackName,
            Source = source,
            Vehicles = vehicles,
        };
    }

    private static Rf2TelemetryRaw ReadPayload(ReadOnlySpan<byte> payload)
    {
        var payloadSize = Marshal.SizeOf<Rf2TelemetryRaw>();
        if (payload.Length < payloadSize)
        {
            throw new InvalidDataException($"Telemetry payload is {payload.Length} bytes; expected at least {payloadSize} bytes.");
        }

        var buffer = payload[..payloadSize].ToArray();
        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<Rf2TelemetryRaw>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    private static bool IsFinite(double value) => double.IsFinite(value);

    private static bool IsPlausibleText(string value) => !string.IsNullOrWhiteSpace(value)
        && value.Any(char.IsLetterOrDigit);

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
internal struct Rf2WheelRaw
{
    public double SuspensionDeflection;
    public double RideHeight;
    public double SuspensionForce;
    public double BrakeTemperature;
    public double BrakePressure;
    public double Rotation;
    public double LateralPatchVelocity;
    public double LongitudinalPatchVelocity;
    public double LateralGroundVelocity;
    public double LongitudinalGroundVelocity;
    public double Camber;
    public double LateralForce;
    public double LongitudinalForce;
    public double TireLoad;
    public double GripFraction;
    public double Pressure;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] Temperature;
    public double Wear;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
    public byte[] TerrainName;
    public byte SurfaceType;
    public byte Flat;
    public byte Detached;
    public byte StaticUndeflectedRadius;
    public double VerticalTireDeflection;
    public double WheelYLocation;
    public double Toe;
    public double TireCarcassTemperature;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public double[] TireInnerLayerTemperature;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 24)]
    public byte[] Expansion;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct Rf2VehicleTelemetryRaw
{
    public int ID;
    public double DeltaTime;
    public double ElapsedTime;
    public int LapNumber;
    public double LapStartET;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] VehicleName;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public byte[] TrackName;
    public Rf2Vec3Raw Pos;
    public Rf2Vec3Raw LocalVel;
    public Rf2Vec3Raw LocalAccel;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public Rf2Vec3Raw[] Ori;
    public Rf2Vec3Raw LocalRot;
    public Rf2Vec3Raw LocalRotAccel;
    public int Gear;
    public double EngineRPM;
    public double EngineWaterTemp;
    public double EngineOilTemp;
    public double ClutchRPM;
    public double UnfilteredThrottle;
    public double UnfilteredBrake;
    public double UnfilteredSteering;
    public double UnfilteredClutch;
    public double FilteredThrottle;
    public double FilteredBrake;
    public double FilteredSteering;
    public double FilteredClutch;
    public double SteeringShaftTorque;
    public double FrontThirdDeflection;
    public double RearThirdDeflection;
    public double FrontWingHeight;
    public double FrontRideHeight;
    public double RearRideHeight;
    public double Drag;
    public double FrontDownforce;
    public double RearDownforce;
    public double Fuel;
    public double EngineMaxRPM;
    public byte ScheduledStops;
    public byte Overheating;
    public byte Detached;
    public byte Headlights;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] DentSeverity;
    public double LastImpactET;
    public double LastImpactMagnitude;
    public Rf2Vec3Raw LastImpactPos;
    public double EngineTorque;
    public int CurrentSector;
    public byte SpeedLimiter;
    public byte MaxGears;
    public byte FrontTireCompoundIndex;
    public byte RearTireCompoundIndex;
    public double FuelCapacity;
    public byte FrontFlapActivated;
    public byte RearFlapActivated;
    public byte RearFlapLegalStatus;
    public byte IgnitionStarter;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 18)]
    public byte[] FrontTireCompoundName;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 18)]
    public byte[] RearTireCompoundName;
    public byte SpeedLimiterAvailable;
    public byte AntiStallActivated;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
    public byte[] Unused;
    public float VisualSteeringWheelRange;
    public double RearBrakeBias;
    public double TurboBoostPressure;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
    public float[] PhysicsToGraphicsOffset;
    public float PhysicalSteeringWheelRange;
    public double BatteryChargeFraction;
    public double ElectricBoostMotorTorque;
    public double ElectricBoostMotorRPM;
    public double ElectricBoostMotorTemperature;
    public double ElectricBoostWaterTemperature;
    public byte ElectricBoostMotorState;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 111)]
    public byte[] Expansion;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
    public Rf2WheelRaw[] Wheels;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct Rf2TelemetryRaw
{
    public uint VersionUpdateBegin;
    public uint VersionUpdateEnd;
    public int BytesUpdatedHint;
    public int NumVehicles;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 128)]
    public Rf2VehicleTelemetryRaw[] Vehicles;
}