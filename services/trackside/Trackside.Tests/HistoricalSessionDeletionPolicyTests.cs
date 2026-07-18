using Trackside.Application.Persistence;
using Trackside.Domain.LiveSession;
using Trackside.Service.Api;

namespace Trackside.Tests;

/// <summary>
/// Verifies policy safeguards for deleting persisted historical sessions.
/// </summary>
public sealed class HistoricalSessionDeletionPolicyTests
{
    /// <summary>
    /// Old sessions remain deletable when no active live snapshot is available.
    /// </summary>
    [Fact]
    public void AllowsDeletionForOldSessionWithoutLiveSnapshot()
    {
        var nowUtc = DateTimeOffset.Parse("2026-07-18T12:00:00+00:00");
        var session = BuildSession(lastSeenUtc: nowUtc.AddHours(-4));

        var decision = HistoricalSessionDeletionPolicy.Evaluate(session, liveSnapshot: null, nowUtc);

        Assert.True(decision.IsAllowed);
        Assert.Equal(HistoricalSessionDeletionBlockReason.None, decision.Reason);
    }

    /// <summary>
    /// Sessions seen inside the 3-hour window are protected from deletion.
    /// </summary>
    [Fact]
    public void BlocksDeletionForRecentSession()
    {
        var nowUtc = DateTimeOffset.Parse("2026-07-18T12:00:00+00:00");
        var session = BuildSession(lastSeenUtc: nowUtc.AddHours(-2));

        var decision = HistoricalSessionDeletionPolicy.Evaluate(session, liveSnapshot: null, nowUtc);

        Assert.False(decision.IsAllowed);
        Assert.Equal(HistoricalSessionDeletionBlockReason.RecentWindow, decision.Reason);
    }

    /// <summary>
    /// A fresh persisted row that matches a fresh active live snapshot is protected as active.
    /// </summary>
    [Fact]
    public void BlocksDeletionForFreshActiveMatchingLiveSession()
    {
        var nowUtc = DateTimeOffset.Parse("2026-07-18T12:00:00+00:00");
        var session = BuildSession(
            lastSeenUtc: nowUtc.AddMinutes(-5),
            trackName: "Loch Drummond - Short",
            sessionKind: SessionKind.Race);
        var liveSnapshot = BuildLiveSnapshot(
            timestampUtc: nowUtc.AddMinutes(-3),
            trackName: "Loch Drummond - Short",
            sessionKind: SessionKind.Race,
            phase: SessionPhase.GreenFlag);

        var decision = HistoricalSessionDeletionPolicy.Evaluate(session, liveSnapshot, nowUtc);

        Assert.False(decision.IsAllowed);
        Assert.Equal(HistoricalSessionDeletionBlockReason.ActiveLiveSession, decision.Reason);
    }

    /// <summary>
    /// An older archive with the same track and kind as the active session remains deletable.
    /// </summary>
    [Fact]
    public void AllowsDeletionForOldSessionMatchingFreshActiveLiveSession()
    {
        var nowUtc = DateTimeOffset.Parse("2026-07-18T12:00:00+00:00");
        var session = BuildSession(
            lastSeenUtc: nowUtc.AddHours(-6),
            trackName: "Loch Drummond - Short",
            sessionKind: SessionKind.Race);
        var liveSnapshot = BuildLiveSnapshot(
            timestampUtc: nowUtc.AddMinutes(-3),
            trackName: "Loch Drummond - Short",
            sessionKind: SessionKind.Race,
            phase: SessionPhase.GreenFlag);

        var decision = HistoricalSessionDeletionPolicy.Evaluate(session, liveSnapshot, nowUtc);

        Assert.True(decision.IsAllowed);
        Assert.Equal(HistoricalSessionDeletionBlockReason.None, decision.Reason);
    }

    /// <summary>
    /// Matching snapshots older than the freshness window do not block deletion.
    /// </summary>
    [Fact]
    public void AllowsDeletionWhenMatchingLiveSnapshotIsStale()
    {
        var nowUtc = DateTimeOffset.Parse("2026-07-18T12:00:00+00:00");
        var session = BuildSession(lastSeenUtc: nowUtc.AddHours(-8));
        var liveSnapshot = BuildLiveSnapshot(
            timestampUtc: nowUtc.AddMinutes(-20),
            trackName: "Loch Drummond - Short",
            sessionKind: SessionKind.Practice,
            phase: SessionPhase.GreenFlag);

        var decision = HistoricalSessionDeletionPolicy.Evaluate(session, liveSnapshot, nowUtc);

        Assert.True(decision.IsAllowed);
        Assert.Equal(HistoricalSessionDeletionBlockReason.None, decision.Reason);
    }

    /// <summary>
    /// Fresh snapshots in non-active phases do not block deletion.
    /// </summary>
    [Theory]
    [InlineData(SessionPhase.SessionOver)]
    [InlineData(SessionPhase.Garage)]
    [InlineData(SessionPhase.Unknown)]
    public void AllowsDeletionWhenLivePhaseIsNotActive(SessionPhase phase)
    {
        var nowUtc = DateTimeOffset.Parse("2026-07-18T12:00:00+00:00");
        var session = BuildSession(lastSeenUtc: nowUtc.AddHours(-8));
        var liveSnapshot = BuildLiveSnapshot(
            timestampUtc: nowUtc.AddMinutes(-2),
            trackName: "Loch Drummond - Short",
            sessionKind: SessionKind.Practice,
            phase: phase);

        var decision = HistoricalSessionDeletionPolicy.Evaluate(session, liveSnapshot, nowUtc);

        Assert.True(decision.IsAllowed);
        Assert.Equal(HistoricalSessionDeletionBlockReason.None, decision.Reason);
    }

    /// <summary>
    /// Fresh active snapshots only block when both track and session kind match.
    /// </summary>
    [Fact]
    public void AllowsDeletionWhenLiveSnapshotDoesNotMatchTrackOrSessionKind()
    {
        var nowUtc = DateTimeOffset.Parse("2026-07-18T12:00:00+00:00");
        var session = BuildSession(
            lastSeenUtc: nowUtc.AddHours(-8),
            trackName: "Loch Drummond - Short",
            sessionKind: SessionKind.Practice);

        var otherTrackSnapshot = BuildLiveSnapshot(
            timestampUtc: nowUtc.AddMinutes(-2),
            trackName: "Mills Metropark",
            sessionKind: SessionKind.Practice,
            phase: SessionPhase.GreenFlag);
        var otherKindSnapshot = BuildLiveSnapshot(
            timestampUtc: nowUtc.AddMinutes(-2),
            trackName: "Loch Drummond - Short",
            sessionKind: SessionKind.Race,
            phase: SessionPhase.GreenFlag);

        var trackDecision = HistoricalSessionDeletionPolicy.Evaluate(session, otherTrackSnapshot, nowUtc);
        var kindDecision = HistoricalSessionDeletionPolicy.Evaluate(session, otherKindSnapshot, nowUtc);

        Assert.True(trackDecision.IsAllowed);
        Assert.Equal(HistoricalSessionDeletionBlockReason.None, trackDecision.Reason);
        Assert.True(kindDecision.IsAllowed);
        Assert.Equal(HistoricalSessionDeletionBlockReason.None, kindDecision.Reason);
    }

    private static HistoricalSessionSummary BuildSession(
        DateTimeOffset lastSeenUtc,
        string trackName = "Loch Drummond - Short",
        SessionKind sessionKind = SessionKind.Practice)
    {
        return new HistoricalSessionSummary
        {
            SessionId = "session-1",
            Source = "shared-memory",
            TrackName = trackName,
            SessionKind = sessionKind,
            SessionPhase = SessionPhase.SessionOver,
            FirstSeenUtc = lastSeenUtc.AddMinutes(-20),
            LastSeenUtc = lastSeenUtc,
        };
    }

    private static LiveSessionSnapshot BuildLiveSnapshot(
        DateTimeOffset timestampUtc,
        string trackName,
        SessionKind sessionKind,
        SessionPhase phase)
    {
        return new LiveSessionSnapshot
        {
            Source = "shared-memory",
            Status = "live",
            TimestampUtc = timestampUtc,
            Session = new LiveSessionInfo
            {
                TrackName = trackName,
                Kind = sessionKind,
                Phase = phase,
            },
        };
    }
}
