using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;

namespace Trackside.Infrastructure.Rf2.SharedMemory;

/// <summary>
/// Reads raw bytes from rFactor 2 shared-memory maps.
/// </summary>
public sealed class Rf2SharedMemoryMapReader
{
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
        var errors = new List<string>();
        foreach (var candidateName in candidateNames)
        {
            try
            {
                return new Rf2MappedBufferRead(candidateName, ReadMap(candidateName, payloadSize + MappedBufferPayloadLocator.VersionBlockSizeBytes));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FileNotFoundException or ArgumentException)
            {
                try
                {
                    return new Rf2MappedBufferRead(candidateName, ReadMap(candidateName, payloadSize));
                }
                catch (Exception fallbackEx) when (fallbackEx is IOException or UnauthorizedAccessException or FileNotFoundException or ArgumentException)
                {
                    errors.Add($"{candidateName}: {fallbackEx.Message}");
                }
            }
        }

        throw new SharedMemoryUnavailableException($"Could not open any rF2 shared-memory map. Tried: {string.Join("; ", errors)}");
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