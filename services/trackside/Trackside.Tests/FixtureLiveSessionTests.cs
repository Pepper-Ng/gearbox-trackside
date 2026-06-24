using System.Text.Json;
using Trackside.Application.LiveSession;
using Trackside.Application.Serialization;
using Trackside.Domain.LiveSession;

namespace Trackside.Tests;

/// <summary>
/// Verifies that the checked-in Phase 0B fixture remains compatible with the browser-facing model.
/// </summary>
public sealed class FixtureLiveSessionTests
{
    /// <summary>
    /// Ensures fixture JSON can be deserialized into the normalized live-session contract.
    /// </summary>
    [Fact]
    public void FixtureDeserializesToNormalizedSnapshot()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "mock-live-session.json");
        var payload = File.ReadAllText(fixturePath);

        var snapshot = JsonSerializer.Deserialize<LiveSessionSnapshot>(payload, TracksideJson.SerializerOptions);

        Assert.NotNull(snapshot);
        Assert.Equal("Loch Drummond - Short", snapshot.Session.TrackName);
        Assert.Equal(SessionKind.Practice, snapshot.Session.Kind);
        Assert.Equal(SessionPhase.GreenFlag, snapshot.Session.Phase);
        Assert.Equal(3, snapshot.Drivers.Count);
        Assert.Equal("Setup1", snapshot.Drivers[0].RigName);
    }

    /// <summary>
    /// Ensures the Phase 1 raw scoring fixture can be converted through the leaderboard builder.
    /// </summary>
    [Fact]
    public void RawScoringFixtureBuildsNormalizedLeaderboard()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "scoring-leaderboard-practice.json");
        var payload = File.ReadAllText(fixturePath);

        var source = JsonSerializer.Deserialize<LeaderboardSourceSnapshot>(payload, TracksideJson.SerializerOptions);
        var snapshot = new LeaderboardSnapshotBuilder().Build(
            source!,
            new DriverAliasMap(new Dictionary<string, string> { ["Setup1"] = "Maya" }),
            DateTimeOffset.Parse("2026-06-24T12:00:00+00:00"),
            updateSequence: 7);

        Assert.NotNull(source);
        Assert.Equal("Loch Drummond - Short", snapshot.Session.TrackName);
        Assert.Equal(4, snapshot.Drivers.Count);
        Assert.Equal("Setup3", snapshot.Drivers[0].RigName);
        Assert.Equal(1, snapshot.Drivers[0].LeaderboardRank);
        Assert.Equal("Maya", snapshot.Drivers.Single(driver => driver.RigName == "Setup1").DisplayName);
    }
}