using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Trackside.Infrastructure.Rf2.SharedMemory;

/// <summary>
/// Reads raw bytes from rFactor 2 shared-memory maps.
/// </summary>
public sealed class Rf2SharedMemoryMapReader
{
    private const uint FileMapRead = 0x0004;

    /// <summary>
    /// Base scoring map name used by the rF2 shared-memory plugin.
    /// </summary>
    public const string ScoringMapName = "$rFactor2SMMP_Scoring$";

    /// <summary>
    /// Base telemetry map name used by the rF2 shared-memory plugin.
    /// </summary>
    public const string TelemetryMapName = "$rFactor2SMMP_Telemetry$";

    /// <summary>
    /// Returns candidate map names for a scoring or telemetry source.
    /// </summary>
    /// <param name="baseName">Base map name from the shared-memory plugin.</param>
    /// <param name="explicitMapName">Optional explicit map name.</param>
    /// <param name="processId">Optional dedicated-server process id.</param>
    /// <returns>Ordered candidate map names.</returns>
    public static IReadOnlyList<string> CandidateMapNames(string baseName, string? explicitMapName, int? processId)
    {
        var names = new List<string>();
        if (!string.IsNullOrWhiteSpace(explicitMapName))
        {
            names.Add(explicitMapName);
        }

        if (processId is not null)
        {
            names.Add($"{baseName}{processId.Value}");
            names.Add($"Global\\{baseName}{processId.Value}");
        }

        names.Add(baseName);

        return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Reads the first available candidate map into memory.
    /// </summary>
    /// <param name="candidateNames">Candidate map names to try in order.</param>
    /// <param name="payloadSize">Expected payload size without the mapped-buffer wrapper.</param>
    /// <returns>Raw mapped-buffer bytes and the map name that produced them.</returns>
    [SupportedOSPlatform("windows")]
    public Rf2MappedBufferRead ReadFirstAvailable(IReadOnlyList<string> candidateNames, int payloadSize)
    {
        return TryReadFirstAvailable(candidateNames, payloadSize, out var read, out var status)
            ? read
            : throw new SharedMemoryUnavailableException(status);
    }

    /// <summary>
    /// Attempts to read the first available candidate map without throwing for the normal missing-map case.
    /// </summary>
    /// <param name="candidateNames">Candidate map names to try in order.</param>
    /// <param name="payloadSize">Expected payload size without the mapped-buffer wrapper.</param>
    /// <param name="read">Raw mapped-buffer bytes when a map was read.</param>
    /// <param name="status">Diagnostic status when no map was read.</param>
    /// <returns>True when a candidate map was read.</returns>
    [SupportedOSPlatform("windows")]
    public bool TryReadFirstAvailable(
        IReadOnlyList<string> candidateNames,
        int payloadSize,
        out Rf2MappedBufferRead read,
        out string status)
    {
        var errors = new List<string>();
        foreach (var candidateName in candidateNames)
        {
            if (!CanOpenMap(candidateName))
            {
                errors.Add($"{candidateName}: missing or inaccessible");
                continue;
            }

            if (TryReadMap(candidateName, payloadSize + MappedBufferPayloadLocator.VersionBlockSizeBytes, out var wrappedBuffer, out var wrappedError))
            {
                read = new Rf2MappedBufferRead(candidateName, wrappedBuffer);
                status = "connected";
                return true;
            }

            if (TryReadMap(candidateName, payloadSize, out var unwrappedBuffer, out var unwrappedError))
            {
                read = new Rf2MappedBufferRead(candidateName, unwrappedBuffer);
                status = "connected";
                return true;
            }

            errors.Add($"{candidateName}: {unwrappedError ?? wrappedError ?? "read failed"}");
        }

        read = new Rf2MappedBufferRead(string.Empty, []);
        status = errors.Count == 0
            ? "No rF2 shared-memory map names were available."
            : $"Could not open any rF2 shared-memory map. Tried: {string.Join("; ", errors)}";
        return false;
    }

    /// <summary>
    /// Tests whether a named map can be opened for reading without using exception-based probing.
    /// </summary>
    /// <param name="mapName">Map name to probe.</param>
    /// <returns>True when the map can be opened for reading.</returns>
    [SupportedOSPlatform("windows")]
    public static bool CanOpenMap(string mapName)
    {
        var handle = NativeMethods.OpenFileMapping(FileMapRead, false, mapName);
        if (handle == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.CloseHandle(handle);
        return true;
    }

    [SupportedOSPlatform("windows")]
    private static bool TryReadMap(string mapName, int bytesToRead, out byte[] buffer, out string? error)
    {
        try
        {
            buffer = ReadMap(mapName, bytesToRead);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or ArgumentException)
        {
            buffer = [];
            error = ex.Message;
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static byte[] ReadMap(string mapName, int bytesToRead)
    {
        using var map = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read);
        using var accessor = map.CreateViewAccessor(0, bytesToRead, MemoryMappedFileAccess.Read);
        var buffer = new byte[bytesToRead];
        accessor.ReadArray(0, buffer, 0, buffer.Length);
        return buffer;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr OpenFileMapping(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);
    }
}

/// <summary>
/// Raw mapped-buffer bytes read from an rF2 shared-memory map.
/// </summary>
/// <param name="MapName">Map name that produced the bytes.</param>
/// <param name="Buffer">Copied map bytes.</param>
public sealed record Rf2MappedBufferRead(string MapName, byte[] Buffer);

/// <summary>
/// Raised when no rFactor 2 shared-memory map can be opened.
/// </summary>
public sealed class SharedMemoryUnavailableException : Exception
{
    /// <summary>
    /// Creates a shared-memory unavailable error.
    /// </summary>
    /// <param name="message">Diagnostic error message.</param>
    public SharedMemoryUnavailableException(string message)
        : base(message)
    {
    }
}