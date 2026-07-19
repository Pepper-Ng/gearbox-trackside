namespace Trackside.Application.LiveSession;

/// <summary>
/// Deterministically allocates visibly distinct Tracker colours to participants in join order.
/// </summary>
public static class DriverColorAllocator
{
    /// <summary>
    /// Fixed, venue-wide palette. The first entries are deliberately the most visually distinct colours.
    /// </summary>
    public static readonly IReadOnlyList<string> Palette =
    [
        "#ef233c", // Red
        "#ff7a00", // Orange
        "#1684ff", // Blue
        "#ffd60a", // Yellow
        "#9b5de5", // Purple
        "#22c55e", // Green
        "#f8fafc", // White
        "#ff00a8", // Magenta
        "#00d9ff", // Cyan
        "#a3e635", // Lime
        "#800000", // Maroon
        "#ff6b6b",
        "#14b8a6",
        "#c084fc",
        "#a16207",
        "#fde68a",
        "#1d4ed8",
        "#94a3b8",
        "#f9a8d4",
        "#84cc16",
    ];

    /// <summary>
    /// Allocates colours by claiming available historical matches first, then filling palette gaps in join order.
    /// </summary>
    /// <param name="participants">Active or reserved session participants.</param>
    /// <param name="history">Previously persisted colour assignments keyed by participant name.</param>
    /// <returns>Assignments keyed by the caller's stable participant id.</returns>
    public static IReadOnlyDictionary<string, string> Allocate(
        IEnumerable<DriverColorParticipant> participants,
        IReadOnlyDictionary<string, string> history)
    {
        ArgumentNullException.ThrowIfNull(participants);
        ArgumentNullException.ThrowIfNull(history);

        var ordered = participants
            .Where(participant => !string.IsNullOrWhiteSpace(participant.ParticipantId)
                && !string.IsNullOrWhiteSpace(participant.HistoryKey))
            .GroupBy(participant => participant.ParticipantId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderBy(participant => participant.JoinSequence).First())
            .OrderBy(participant => participant.JoinSequence)
            .ThenBy(participant => participant.ParticipantId, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var assignments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var claimed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Claim pass: earlier joiners retain a historical palette colour whenever it is not already claimed this pass.
        foreach (var participant in ordered)
        {
            if (history.TryGetValue(participant.HistoryKey, out var historicalColor)
                && Palette.Contains(historicalColor, StringComparer.OrdinalIgnoreCase)
                && claimed.Add(historicalColor))
            {
                assignments[participant.ParticipantId] = historicalColor;
            }
        }

        // Fill pass: new drivers, or a later claimant that lost a historical collision, take the first free palette colour.
        foreach (var participant in ordered.Where(participant => !assignments.ContainsKey(participant.ParticipantId)))
        {
            var color = Palette.FirstOrDefault(candidate => !claimed.Contains(candidate));
            if (color is null)
            {
                // Venue sessions are expected to remain within the fixed twenty-colour palette. This graceful fallback
                // keeps the display usable rather than refusing a vehicle when a source reports more than twenty cars.
                color = Palette[assignments.Count % Palette.Count];
            }

            claimed.Add(color);
            assignments[participant.ParticipantId] = color;
        }

        return assignments;
    }
}

/// <summary>
/// Stable input to the Tracker colour allocator.
/// </summary>
public sealed record DriverColorParticipant
{
    /// <summary>Stable source identifier for the currently connected vehicle.</summary>
    public string ParticipantId { get; init; } = string.Empty;

    /// <summary>Persistent name key used for history lookup and update.</summary>
    public string HistoryKey { get; init; } = string.Empty;

    /// <summary>Monotonic first-seen order within the live session.</summary>
    public long JoinSequence { get; init; }
}
