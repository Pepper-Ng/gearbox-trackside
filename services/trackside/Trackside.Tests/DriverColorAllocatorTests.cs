using Trackside.Application.LiveSession;

namespace Trackside.Tests;

/// <summary>
/// Verifies the deterministic two-pass Tracker colour allocation policy.
/// </summary>
public sealed class DriverColorAllocatorTests
{
    [Fact]
    public void ClaimsHistoricalColoursByJoinOrderThenFillsPaletteGaps()
    {
        var assignments = DriverColorAllocator.Allocate(
        [
            Participant("early", "Maya", 1),
            Participant("new", "Noah", 2),
            Participant("late", "Lina", 3),
        ],
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Maya"] = "#1684ff",
            ["Lina"] = "#1684ff",
        });

        Assert.Equal("#1684ff", assignments["early"]);
        Assert.Equal("#ef233c", assignments["new"]);
        Assert.Equal("#ff7a00", assignments["late"]);
    }

    [Fact]
    public void KeepsAnExistingSessionAllocationWhenASecondParticipantJoins()
    {
        var history = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Maya"] = "#ef233c",
        };
        var first = DriverColorAllocator.Allocate([Participant("maya", "Maya", 1)], history);
        history["Maya"] = first["maya"];

        var second = DriverColorAllocator.Allocate(
        [
            Participant("maya", "Maya", 1),
            Participant("noah", "Noah", 2),
        ],
        history);

        Assert.Equal(first["maya"], second["maya"]);
        Assert.Equal("#ff7a00", second["noah"]);
    }

    private static DriverColorParticipant Participant(string id, string name, long joinSequence) => new()
    {
        ParticipantId = id,
        HistoryKey = name,
        JoinSequence = joinSequence,
    };
}
