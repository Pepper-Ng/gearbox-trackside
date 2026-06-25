using System.Text.Json;
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
}