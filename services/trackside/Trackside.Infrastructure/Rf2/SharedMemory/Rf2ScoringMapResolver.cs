using Trackside.Application.Configuration;

namespace Trackside.Infrastructure.Rf2.SharedMemory;

/// <summary>
/// Applies configuration and ambiguity policy to discovered rFactor 2 scoring maps.
/// </summary>
public sealed class Rf2ScoringMapResolver
{
    /// <summary>
    /// Builds the ordered scoring map candidate list for one read attempt.
    /// </summary>
    /// <param name="options">Shared-memory source options.</param>
    /// <param name="discoveredMaps">Auto-discovered visible scoring maps.</param>
    /// <returns>Resolved candidates or an ambiguity result.</returns>
    public Rf2ScoringMapResolution Resolve(
        TracksideSharedMemoryOptions options,
        IReadOnlyList<Rf2ScoringMapCandidate> discoveredMaps)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(discoveredMaps);

        if (!string.IsNullOrWhiteSpace(options.ScoringMapName))
        {
            return Rf2ScoringMapResolution.Ready(
                [options.ScoringMapName.Trim()],
                "explicit scoring map configured");
        }

        if (options.ProcessId is not null)
        {
            return Rf2ScoringMapResolution.Ready(
                Rf2SharedMemoryMapReader.CandidateMapNames(
                    Rf2SharedMemoryMapReader.ScoringMapName,
                    explicitMapName: null,
                    options.ProcessId),
                $"explicit process id configured: {options.ProcessId}");
        }

        if (options.AutoDiscover && discoveredMaps.Count > 0)
        {
            var pidGroups = discoveredMaps
                .Where(candidate => candidate.ProcessId is not null)
                .GroupBy(candidate => candidate.ProcessId!.Value)
                .OrderBy(group => group.Key)
                .ToList();

            if (pidGroups.Count > 1 && options.MultipleScoringMapPolicy == TracksideMultipleScoringMapPolicy.RequireExplicitSelection)
            {
                return Rf2ScoringMapResolution.Ambiguous(
                    discoveredMaps,
                    "Multiple PID-suffixed rFactor 2 scoring maps are visible. Configure a process id or exact scoring map name.");
            }

            var selected = pidGroups.Count > 0
                ? pidGroups.First().ToList()
                : discoveredMaps.ToList();

            return Rf2ScoringMapResolution.Ready(
                selected.Select(candidate => candidate.MapName).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                pidGroups.Count > 1 ? "multiple maps found; first discovered selected by policy" : "auto-discovered scoring map");
        }

        return Rf2ScoringMapResolution.Ready(
            Rf2SharedMemoryMapReader.CandidateMapNames(
                Rf2SharedMemoryMapReader.ScoringMapName,
                explicitMapName: null,
                processId: null),
            options.AutoDiscover ? "auto-discovery found no scoring maps; trying base client map" : "auto-discovery disabled; trying base client map");
    }
}

/// <summary>
/// One candidate rFactor 2 scoring map found through process hints or Section enumeration.
/// </summary>
/// <param name="MapName">Map name that can be passed to OpenFileMapping.</param>
/// <param name="ProcessId">PID suffix extracted from the map name when present.</param>
/// <param name="ProcessName">Matched process name when known.</param>
/// <param name="DiscoverySource">How the candidate was discovered.</param>
public sealed record Rf2ScoringMapCandidate(string MapName, int? ProcessId, string? ProcessName, string DiscoverySource);

/// <summary>
/// Result of resolving scoring maps for a read attempt.
/// </summary>
public sealed record Rf2ScoringMapResolution
{
    private Rf2ScoringMapResolution(
        bool isAmbiguous,
        IReadOnlyList<string> candidateMapNames,
        IReadOnlyList<Rf2ScoringMapCandidate> ambiguousCandidates,
        string status)
    {
        IsAmbiguous = isAmbiguous;
        CandidateMapNames = candidateMapNames;
        AmbiguousCandidates = ambiguousCandidates;
        Status = status;
    }

    /// <summary>
    /// True when discovery found multiple live dedicated-server scoring maps and policy requires explicit selection.
    /// </summary>
    public bool IsAmbiguous { get; }

    /// <summary>
    /// Ordered candidate map names to try when not ambiguous.
    /// </summary>
    public IReadOnlyList<string> CandidateMapNames { get; }

    /// <summary>
    /// Candidate list that caused ambiguity.
    /// </summary>
    public IReadOnlyList<Rf2ScoringMapCandidate> AmbiguousCandidates { get; }

    /// <summary>
    /// Human-readable resolution status.
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Creates a ready resolution.
    /// </summary>
    /// <param name="candidateMapNames">Map names to try.</param>
    /// <param name="status">Diagnostic status.</param>
    /// <returns>A ready map resolution.</returns>
    public static Rf2ScoringMapResolution Ready(IReadOnlyList<string> candidateMapNames, string status) =>
        new(false, candidateMapNames, [], status);

    /// <summary>
    /// Creates an ambiguous resolution.
    /// </summary>
    /// <param name="ambiguousCandidates">Maps that require explicit selection.</param>
    /// <param name="status">Diagnostic status.</param>
    /// <returns>An ambiguous map resolution.</returns>
    public static Rf2ScoringMapResolution Ambiguous(IReadOnlyList<Rf2ScoringMapCandidate> ambiguousCandidates, string status) =>
        new(true, [], ambiguousCandidates, status);
}