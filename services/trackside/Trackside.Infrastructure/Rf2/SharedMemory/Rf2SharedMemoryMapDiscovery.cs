using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Trackside.Application.Configuration;

namespace Trackside.Infrastructure.Rf2.SharedMemory;

/// <summary>
/// Discovers visible rFactor 2 shared-memory scoring maps without requiring a pre-known dedicated-server PID.
/// </summary>
public interface IRf2SharedMemoryMapDiscovery
{
    /// <summary>
    /// Finds visible scoring map candidates using process-name hints and Windows Section enumeration.
    /// </summary>
    /// <param name="options">Shared-memory configuration.</param>
    /// <returns>Discovered scoring map candidates.</returns>
    IReadOnlyList<Rf2ScoringMapCandidate> DiscoverScoringMaps(TracksideSharedMemoryOptions options);
}

/// <summary>
/// Windows implementation of rFactor 2 shared-memory map discovery.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class Rf2SharedMemoryMapDiscovery : IRf2SharedMemoryMapDiscovery
{
    private const string BaseNamedObjectsDirectory = @"\BaseNamedObjects";
    private const string SectionTypeName = "Section";
    private const uint DirectoryQuery = 0x0001;
    private const uint ObjectCaseInsensitive = 0x00000040;
    private const uint StatusNoMoreEntries = 0x8000001A;

    /// <inheritdoc />
    public IReadOnlyList<Rf2ScoringMapCandidate> DiscoverScoringMaps(TracksideSharedMemoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var processCandidates = DiscoverProcessCandidates(options.DedicatedServerProcessNames).ToList();
        var processSessionIds = processCandidates
            .Select(candidate => candidate.ProcessId)
            .Where(processId => processId is not null)
            .Select(processId => TryGetProcessSessionId(processId!.Value))
            .Where(sessionId => sessionId is not null)
            .Select(sessionId => sessionId!.Value)
            .ToList();

        var sectionCandidates = EnumerateSectionCandidates(processSessionIds).ToList();

        return processCandidates
            .Concat(sectionCandidates)
            .GroupBy(candidate => candidate.MapName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(candidate => candidate.ProcessName is not null).First())
            .OrderBy(candidate => candidate.ProcessId is null ? 1 : 0)
            .ThenBy(candidate => candidate.ProcessId ?? int.MaxValue)
            .ThenBy(candidate => candidate.MapName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<Rf2ScoringMapCandidate> DiscoverProcessCandidates(IReadOnlyList<string> configuredProcessNames)
    {
        var processNameHints = configuredProcessNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(NormalizeProcessName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (processNameHints.Count == 0)
        {
            yield break;
        }

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                string processName;
                try
                {
                    processName = process.ProcessName;
                }
                catch (InvalidOperationException)
                {
                    continue;
                }

                var normalizedProcessName = NormalizeProcessName(processName);
                if (!processNameHints.Any(hint => normalizedProcessName.Equals(hint, StringComparison.OrdinalIgnoreCase)
                    || normalizedProcessName.Contains(hint, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                foreach (var mapName in new[]
                {
                    $"{Rf2SharedMemoryMapReader.ScoringMapName}{process.Id}",
                    $"Global\\{Rf2SharedMemoryMapReader.ScoringMapName}{process.Id}",
                })
                {
                    if (Rf2SharedMemoryMapReader.CanOpenMap(mapName))
                    {
                        yield return new Rf2ScoringMapCandidate(mapName, process.Id, processName, "process-name");
                    }
                }
            }
        }
    }

    private static IEnumerable<Rf2ScoringMapCandidate> EnumerateSectionCandidates(IReadOnlyList<int> processSessionIds)
    {
        foreach (var directory in VisibleObjectDirectories(processSessionIds))
        {
            foreach (var entry in EnumerateDirectory(directory))
            {
                if (!string.Equals(entry.TypeName, SectionTypeName, StringComparison.OrdinalIgnoreCase)
                    || !entry.Name.StartsWith(Rf2SharedMemoryMapReader.ScoringMapName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var processId = ExtractPidSuffix(entry.Name);
                yield return new Rf2ScoringMapCandidate(entry.Name, processId, null, $"section:{directory}");

                if (string.Equals(directory, BaseNamedObjectsDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    yield return new Rf2ScoringMapCandidate($"Global\\{entry.Name}", processId, null, $"section:{directory}");
                }
            }
        }
    }

    private static IEnumerable<string> VisibleObjectDirectories(IReadOnlyList<int> processSessionIds)
    {
        yield return BaseNamedObjectsDirectory;

        foreach (var sessionId in new[] { TryGetProcessSessionId(Environment.ProcessId) }
            .Concat(processSessionIds.Select(sessionId => (int?)sessionId))
            .Concat([0])
            .Where(sessionId => sessionId is >= 0)
            .Select(sessionId => sessionId!.Value)
            .Distinct()
            .OrderBy(sessionId => sessionId))
        {
            yield return $@"\Sessions\{sessionId}\BaseNamedObjects";
        }
    }

    private static IReadOnlyList<ObjectDirectoryEntry> EnumerateDirectory(string directory)
    {
        var objectName = new NativeUnicodeString(directory);
        var attributes = new ObjectAttributes
        {
            Length = Marshal.SizeOf<ObjectAttributes>(),
            RootDirectory = IntPtr.Zero,
            ObjectName = objectName.Pointer,
            Attributes = ObjectCaseInsensitive,
            SecurityDescriptor = IntPtr.Zero,
            SecurityQualityOfService = IntPtr.Zero,
        };

        var status = NativeMethods.NtOpenDirectoryObject(out var handle, DirectoryQuery, ref attributes);
        objectName.Dispose();
        if (status < 0)
        {
            return [];
        }

        try
        {
            var context = 0u;
            var restartScan = true;
            var entries = new List<ObjectDirectoryEntry>();

            while (true)
            {
                var buffer = Marshal.AllocHGlobal(64 * 1024);
                try
                {
                    status = NativeMethods.NtQueryDirectoryObject(
                        handle,
                        buffer,
                        64 * 1024,
                        returnSingleEntry: true,
                        restartScan,
                        ref context,
                        out _);
                    restartScan = false;

                    if (unchecked((uint)status) == StatusNoMoreEntries)
                    {
                        break;
                    }

                    if (status < 0)
                    {
                        break;
                    }

                    var information = Marshal.PtrToStructure<ObjectDirectoryInformation>(buffer);
                    var name = information.Name.ToText();
                    var typeName = information.TypeName.ToText();
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        entries.Add(new ObjectDirectoryEntry(name, typeName));
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return entries;
        }
        finally
        {
            NativeMethods.CloseHandle(handle);
        }
    }

    private static string NormalizeProcessName(string processName)
    {
        var trimmed = processName.Trim();
        return trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? trimmed[..^4]
            : trimmed;
    }

    private static int? ExtractPidSuffix(string mapName)
    {
        if (!mapName.StartsWith(Rf2SharedMemoryMapReader.ScoringMapName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var suffix = mapName[Rf2SharedMemoryMapReader.ScoringMapName.Length..];
        return int.TryParse(suffix, out var processId) ? processId : null;
    }

    private static int? TryGetProcessSessionId(int processId)
    {
        return NativeMethods.ProcessIdToSessionId(processId, out var sessionId) ? (int)sessionId : null;
    }

    private sealed record ObjectDirectoryEntry(string Name, string TypeName);

    private sealed class NativeUnicodeString : IDisposable
    {
        private readonly IntPtr _buffer;

        public NativeUnicodeString(string value)
        {
            _buffer = Marshal.StringToHGlobalUni(value);
            Value = new UnicodeString
            {
                Length = checked((ushort)(value.Length * 2)),
                MaximumLength = checked((ushort)((value.Length + 1) * 2)),
                Buffer = _buffer,
            };
            Pointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(Value, Pointer, fDeleteOld: false);
        }

        public UnicodeString Value { get; }

        public IntPtr Pointer { get; }

        public void Dispose()
        {
            Marshal.FreeHGlobal(Pointer);
            Marshal.FreeHGlobal(_buffer);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;

        public readonly string ToText() => Buffer == IntPtr.Zero || Length == 0
            ? string.Empty
            : Marshal.PtrToStringUni(Buffer, Length / 2) ?? string.Empty;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectDirectoryInformation
    {
        public UnicodeString Name;
        public UnicodeString TypeName;
    }

    private static class NativeMethods
    {
        [DllImport("ntdll.dll")]
        public static extern int NtOpenDirectoryObject(out IntPtr directoryHandle, uint desiredAccess, ref ObjectAttributes objectAttributes);

        [DllImport("ntdll.dll")]
        public static extern int NtQueryDirectoryObject(
            IntPtr directoryHandle,
            IntPtr buffer,
            uint length,
            [MarshalAs(UnmanagedType.Bool)] bool returnSingleEntry,
            [MarshalAs(UnmanagedType.Bool)] bool restartScan,
            ref uint context,
            out uint returnLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ProcessIdToSessionId(int processId, out uint sessionId);
    }
}