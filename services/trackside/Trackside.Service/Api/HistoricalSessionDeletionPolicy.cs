using Trackside.Application.Persistence;
using Trackside.Domain.LiveSession;

namespace Trackside.Service.Api;

/// <summary>
/// Evaluates whether a persisted session can be deleted without conflicting with live or recent-session safeguards.
/// </summary>
public static class HistoricalSessionDeletionPolicy
{
    /// <summary>
    /// Session age window considered "recent" for deletion protection.
    /// </summary>
    public static TimeSpan RecentWindow { get; } = TimeSpan.FromHours(3);

    /// <summary>
    /// Maximum live snapshot age used when checking active-session delete protection.
    /// </summary>
    public static TimeSpan LiveSnapshotFreshnessWindow { get; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Determines whether the specified persisted session can be deleted at the current UTC time.
    /// </summary>
    /// <param name="session">Persisted session under consideration.</param>
    /// <param name="liveSnapshot">Current live-session snapshot, if available.</param>
    /// <param name="nowUtc">Current UTC time used for age comparisons.</param>
    /// <returns>Deletion decision and block reason when applicable.</returns>
    public static HistoricalSessionDeletionDecision Evaluate(
        HistoricalSessionSummary session,
        LiveSessionSnapshot? liveSnapshot,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (IsFreshHistoricalSession(session, nowUtc)
            && IsFreshActiveSnapshot(liveSnapshot, nowUtc)
            && IsMatchingTrackAndKind(session, liveSnapshot!))
        {
            return new HistoricalSessionDeletionDecision(
                isAllowed: false,
                reason: HistoricalSessionDeletionBlockReason.ActiveLiveSession);
        }

        if (session.LastSeenUtc >= nowUtc - RecentWindow)
        {
            return new HistoricalSessionDeletionDecision(
                isAllowed: false,
                reason: HistoricalSessionDeletionBlockReason.RecentWindow);
        }

        return new HistoricalSessionDeletionDecision(
            isAllowed: true,
            reason: HistoricalSessionDeletionBlockReason.None);
    }

    private static bool IsFreshHistoricalSession(HistoricalSessionSummary session, DateTimeOffset nowUtc) =>
        (session.LastSeenUtc - nowUtc).Duration() <= LiveSnapshotFreshnessWindow;

    private static bool IsFreshActiveSnapshot(LiveSessionSnapshot? liveSnapshot, DateTimeOffset nowUtc)
    {
        if (liveSnapshot is null)
        {
            return false;
        }

        if ((liveSnapshot.TimestampUtc - nowUtc).Duration() > LiveSnapshotFreshnessWindow)
        {
            return false;
        }

        return liveSnapshot.Session.Phase is not SessionPhase.SessionOver
            and not SessionPhase.Garage
            and not SessionPhase.Unknown;
    }

    private static bool IsMatchingTrackAndKind(HistoricalSessionSummary session, LiveSessionSnapshot liveSnapshot)
    {
        if (session.SessionKind != liveSnapshot.Session.Kind)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(session.TrackName) || string.IsNullOrWhiteSpace(liveSnapshot.Session.TrackName))
        {
            return false;
        }

        return string.Equals(
            session.TrackName.Trim(),
            liveSnapshot.Session.TrackName.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Reason that historical-session deletion is blocked.
/// </summary>
public enum HistoricalSessionDeletionBlockReason
{
    /// <summary>
    /// No deletion block applies.
    /// </summary>
    None,

    /// <summary>
    /// Session is still within the protected recent-results window.
    /// </summary>
    RecentWindow,

    /// <summary>
    /// Session matches an active live session and is protected.
    /// </summary>
    ActiveLiveSession,
}

/// <summary>
/// Outcome of historical-session deletion policy evaluation.
/// </summary>
public readonly record struct HistoricalSessionDeletionDecision
{
    /// <summary>
    /// True when deletion is allowed.
    /// </summary>
    public bool IsAllowed { get; init; }

    /// <summary>
    /// Block reason when deletion is denied.
    /// </summary>
    public HistoricalSessionDeletionBlockReason Reason { get; init; }

    /// <summary>
    /// Creates a new deletion decision value.
    /// </summary>
    /// <param name="isAllowed">True when deletion is allowed.</param>
    /// <param name="reason">Block reason when deletion is denied.</param>
    public HistoricalSessionDeletionDecision(bool isAllowed, HistoricalSessionDeletionBlockReason reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }
}
