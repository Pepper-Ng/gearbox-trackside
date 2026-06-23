using Trackside.Infrastructure.Rf2.SharedMemory;

namespace Trackside.Tests;

/// <summary>
/// Captures the parser-offset behavior learned from the Python PoC.
/// </summary>
public sealed class MappedBufferPayloadLocatorTests
{
    /// <summary>
    /// Verifies that zero-offset payloads remain supported for tests and fallback captures.
    /// </summary>
    [Fact]
    public void LocateReturnsZeroOffsetWhenItScoresHighest()
    {
        var buffer = new byte[] { 9, 1, 2, 3, 0, 0, 0, 0, 1, 2, 3, 4 };
        var locator = new MappedBufferPayloadLocator();

        var location = locator.Locate(buffer, payloadSize: 4, payload => payload[0]);

        Assert.Equal(0, location.Offset);
        Assert.Equal(9, location.Score);
    }

    /// <summary>
    /// Verifies that wrapper-offset payloads are preferred when the version block comes first.
    /// </summary>
    [Fact]
    public void LocateReturnsWrapperOffsetWhenItScoresHighest()
    {
        var buffer = new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 8, 1, 2, 3 };
        var locator = new MappedBufferPayloadLocator();

        var location = locator.Locate(buffer, payloadSize: 4, payload => payload[0]);

        Assert.Equal(MappedBufferPayloadLocator.VersionBlockSizeBytes, location.Offset);
        Assert.Equal(8, location.Score);
    }
}