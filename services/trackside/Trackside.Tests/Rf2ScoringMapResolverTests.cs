using Trackside.Application.Configuration;
using Trackside.Infrastructure.Rf2.SharedMemory;

namespace Trackside.Tests;

/// <summary>
/// Covers shared-memory map selection and ambiguity policy without requiring live rFactor 2 maps.
/// </summary>
public sealed class Rf2ScoringMapResolverTests
{
    /// <summary>
    /// Exact configured map names always win over discovery.
    /// </summary>
    [Fact]
    public void ExplicitScoringMapWins()
    {
        var options = new TracksideSharedMemoryOptions { ScoringMapName = "Global\\$rFactor2SMMP_Scoring$1234" };

        var resolution = new Rf2ScoringMapResolver().Resolve(options, MultipleDiscoveredMaps());

        Assert.False(resolution.IsAmbiguous);
        Assert.Equal(["Global\\$rFactor2SMMP_Scoring$1234"], resolution.CandidateMapNames);
    }

    /// <summary>
    /// Configured process IDs derive both local and global PID-suffixed names.
    /// </summary>
    [Fact]
    public void ExplicitProcessIdBuildsPidSuffixedCandidates()
    {
        var options = new TracksideSharedMemoryOptions { ProcessId = 1234 };

        var resolution = new Rf2ScoringMapResolver().Resolve(options, []);

        Assert.False(resolution.IsAmbiguous);
        Assert.Contains("$rFactor2SMMP_Scoring$1234", resolution.CandidateMapNames);
        Assert.Contains("Global\\$rFactor2SMMP_Scoring$1234", resolution.CandidateMapNames);
    }

    /// <summary>
    /// Multiple discovered dedicated-server maps require explicit selection by default.
    /// </summary>
    [Fact]
    public void MultipleDiscoveredPidMapsAreAmbiguousByDefault()
    {
        var options = new TracksideSharedMemoryOptions();

        var resolution = new Rf2ScoringMapResolver().Resolve(options, MultipleDiscoveredMaps());

        Assert.True(resolution.IsAmbiguous);
        Assert.Empty(resolution.CandidateMapNames);
        Assert.Equal(2, resolution.AmbiguousCandidates.Select(candidate => candidate.ProcessId).Distinct().Count());
    }

    /// <summary>
    /// The diagnostic policy can intentionally select the first discovered PID group.
    /// </summary>
    [Fact]
    public void UseFirstDiscoveredPolicySelectsFirstPidGroup()
    {
        var options = new TracksideSharedMemoryOptions
        {
            MultipleScoringMapPolicy = TracksideMultipleScoringMapPolicy.UseFirstDiscovered,
        };

        var resolution = new Rf2ScoringMapResolver().Resolve(options, MultipleDiscoveredMaps());

        Assert.False(resolution.IsAmbiguous);
        Assert.All(resolution.CandidateMapNames, name => Assert.Contains("1111", name));
    }

    /// <summary>
    /// If discovery finds no maps, the base client map remains a fallback.
    /// </summary>
    [Fact]
    public void FallsBackToBaseScoringMapWhenDiscoveryFindsNothing()
    {
        var options = new TracksideSharedMemoryOptions();

        var resolution = new Rf2ScoringMapResolver().Resolve(options, []);

        Assert.False(resolution.IsAmbiguous);
        Assert.Equal(["$rFactor2SMMP_Scoring$"], resolution.CandidateMapNames);
    }

    private static IReadOnlyList<Rf2ScoringMapCandidate> MultipleDiscoveredMaps() =>
    [
        new("$rFactor2SMMP_Scoring$1111", 1111, "rFactor2 Dedicated", "section"),
        new("Global\\$rFactor2SMMP_Scoring$1111", 1111, "rFactor2 Dedicated", "section"),
        new("$rFactor2SMMP_Scoring$2222", 2222, "rFactor2 Dedicated", "section"),
    ];
}