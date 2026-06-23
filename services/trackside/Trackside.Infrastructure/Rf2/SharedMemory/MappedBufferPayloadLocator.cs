namespace Trackside.Infrastructure.Rf2.SharedMemory;

/// <summary>
/// Locates the payload inside an rF2 shared-memory mapped buffer.
/// </summary>
public sealed class MappedBufferPayloadLocator
{
    /// <summary>
    /// Size in bytes of the rF2 mapped-buffer version block: begin and end counters.
    /// </summary>
    public const int VersionBlockSizeBytes = 8;

    /// <summary>
    /// Finds the best payload offset by scoring zero-offset and wrapper-offset candidates.
    /// </summary>
    /// <param name="buffer">Raw mapped-buffer bytes copied from a source.</param>
    /// <param name="payloadSize">Expected size of the payload structure.</param>
    /// <param name="scorePayload">Function that scores how plausible a candidate payload is.</param>
    /// <returns>The best candidate location and score.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="payloadSize" /> is not positive.</exception>
    /// <exception cref="InvalidOperationException">Thrown when no candidate fits in the supplied buffer.</exception>
    public MappedBufferPayloadLocation Locate(
        ReadOnlySpan<byte> buffer,
        int payloadSize,
        MappedBufferPayloadScore scorePayload)
    {
        ArgumentNullException.ThrowIfNull(scorePayload);

        if (payloadSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payloadSize), "Payload size must be positive.");
        }

        var best = default(MappedBufferPayloadLocation?);
        foreach (var offset in new[] { 0, VersionBlockSizeBytes })
        {
            if (buffer.Length < offset + payloadSize)
            {
                continue;
            }

            var score = scorePayload(buffer.Slice(offset, payloadSize));
            var candidate = new MappedBufferPayloadLocation(offset, payloadSize, score);
            if (best is null || candidate.Score > best.Value.Score)
            {
                best = candidate;
            }
        }

        return best ?? throw new InvalidOperationException("The mapped buffer is smaller than the expected payload.");
    }
}

/// <summary>
/// Location of a candidate payload inside a copied mapped buffer.
/// </summary>
/// <param name="Offset">Byte offset at which the payload begins.</param>
/// <param name="PayloadSize">Expected payload size in bytes.</param>
/// <param name="Score">Plausibility score returned by the payload scorer.</param>
public readonly record struct MappedBufferPayloadLocation(int Offset, int PayloadSize, int Score);

/// <summary>
/// Scores a candidate mapped-buffer payload without copying the underlying bytes.
/// </summary>
/// <param name="payload">Candidate payload slice.</param>
/// <returns>A higher value for a more plausible payload.</returns>
public delegate int MappedBufferPayloadScore(ReadOnlySpan<byte> payload);