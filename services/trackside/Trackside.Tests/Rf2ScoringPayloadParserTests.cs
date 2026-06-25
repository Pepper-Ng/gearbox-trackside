using Trackside.Infrastructure.Rf2.SharedMemory;

namespace Trackside.Tests;

/// <summary>
/// Covers rFactor 2 scoring payload parser guard behavior.
/// </summary>
public sealed class Rf2ScoringPayloadParserTests
{
    /// <summary>
    /// Equal rF2 update counters indicate a stable scoring frame.
    /// </summary>
    [Fact]
    public void IsStablePayloadAcceptsMatchingVersionCounters()
    {
        var payload = new byte[8];
        BitConverter.GetBytes(42u).CopyTo(payload, 0);
        BitConverter.GetBytes(42u).CopyTo(payload, 4);

        Assert.True(new Rf2ScoringPayloadParser().IsStablePayload(payload));
    }

    /// <summary>
    /// Mismatched rF2 update counters indicate a torn scoring frame.
    /// </summary>
    [Fact]
    public void IsStablePayloadRejectsMismatchedVersionCounters()
    {
        var payload = new byte[8];
        BitConverter.GetBytes(42u).CopyTo(payload, 0);
        BitConverter.GetBytes(43u).CopyTo(payload, 4);

        Assert.False(new Rf2ScoringPayloadParser().IsStablePayload(payload));
    }
}