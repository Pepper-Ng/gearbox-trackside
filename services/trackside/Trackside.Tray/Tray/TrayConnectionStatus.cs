namespace Trackside.Tray.Tray;

/// <summary>
/// Coarse service and shared-memory status represented by the tray icon dot.
/// </summary>
public enum TrayConnectionStatus
{
    /// <summary>No shared-memory scoring map is connected, or the service cannot be reached.</summary>
    Disconnected,

    /// <summary>A shared-memory scoring map is connected, but no active session is running.</summary>
    MemoryMapConnected,

    /// <summary>A shared-memory scoring map is connected and an active session is running.</summary>
    ActiveSession,
}

/// <summary>
/// Minimal live-session observation used by the tray status classifier.
/// </summary>
/// <param name="Source">Snapshot source, such as shared-memory or fixture.</param>
/// <param name="Status">Source status text.</param>
/// <param name="Session">Session metadata needed for active-session detection.</param>
public sealed record TrayStatusObservation(string? Source, string? Status, TraySessionObservation? Session);

/// <summary>
/// Minimal session observation used by the tray status classifier.
/// </summary>
/// <param name="Kind">Session kind reported by the backend.</param>
/// <param name="Phase">Session phase reported by the backend.</param>
public sealed record TraySessionObservation(string? Kind, string? Phase);

/// <summary>
/// Converts live-session observations into tray icon status colors.
/// </summary>
public static class TrayStatusClassifier
{
    /// <summary>
    /// Classifies a live-session observation into red, blue, or green tray status.
    /// </summary>
    /// <param name="observation">Latest service observation, or null when the service cannot be reached.</param>
    /// <returns>Tray status for the icon overlay.</returns>
    public static TrayConnectionStatus Classify(TrayStatusObservation? observation)
    {
        if (observation is null || !IsMemoryMapConnected(observation))
        {
            return TrayConnectionStatus.Disconnected;
        }

        return IsActiveSession(observation.Session)
            ? TrayConnectionStatus.ActiveSession
            : TrayConnectionStatus.MemoryMapConnected;
    }

    private static bool IsMemoryMapConnected(TrayStatusObservation observation)
    {
        var status = observation.Status ?? string.Empty;
        return string.Equals(observation.Source, "shared-memory", StringComparison.OrdinalIgnoreCase)
            && StatusIndicatesConnected(status)
            && !status.Contains("waiting", StringComparison.OrdinalIgnoreCase)
            && !status.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
    }

    private static bool StatusIndicatesConnected(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        var trimmed = status.Trim();
        return trimmed.Equals("connected", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("connected ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("connected:", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("; connected", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsActiveSession(TraySessionObservation? session) =>
        session is not null
        && string.Equals(session.Phase, "GreenFlag", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(session.Kind)
        && !string.Equals(session.Kind, "Unknown", StringComparison.OrdinalIgnoreCase);
}