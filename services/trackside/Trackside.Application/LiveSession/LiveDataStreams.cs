using Trackside.Domain.LiveSession;

namespace Trackside.Application.LiveSession;

/// <summary>
/// Publishes projected live data frames to registered application modules.
/// </summary>
public interface ILiveDataPublisher
{
    /// <summary>
    /// Publishes a frame to all consumers registered for that frame type.
    /// </summary>
    /// <typeparam name="TFrame">Frame type.</typeparam>
    /// <param name="frame">Projected live data frame.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes when all consumers have handled the frame.</returns>
    ValueTask PublishAsync<TFrame>(TFrame frame, CancellationToken cancellationToken)
        where TFrame : notnull;
}

/// <summary>
/// Consumes one projected live data frame type.
/// </summary>
/// <typeparam name="TFrame">Frame type consumed by a module.</typeparam>
public interface ILiveDataConsumer<in TFrame>
    where TFrame : notnull
{
    /// <summary>
    /// Handles one projected live data frame.
    /// </summary>
    /// <param name="frame">Projected live data frame.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes after the frame is consumed.</returns>
    ValueTask ConsumeAsync(TFrame frame, CancellationToken cancellationToken);
}

/// <summary>
/// Scoring-derived live context used by modules that need lap progress and validity state.
/// </summary>
public sealed record ScoringContextFrame
{
    /// <summary>
    /// Latest normalized scoring snapshot.
    /// </summary>
    public LiveSessionSnapshot Snapshot { get; init; } = new();
}

/// <summary>
/// High-rate telemetry world positions projected from the full rFactor 2 telemetry map.
/// </summary>
public sealed record TelemetryPositionFrame
{
    /// <summary>
    /// Track name reported by telemetry.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Logical source label.
    /// </summary>
    public string Source { get; init; } = "telemetry";

    /// <summary>
    /// Telemetry vehicle world positions.
    /// </summary>
    public IReadOnlyList<TelemetryPositionVehicle> Vehicles { get; init; } = [];
}

/// <summary>
/// One telemetry vehicle position sample.
/// </summary>
public sealed record TelemetryPositionVehicle
{
    /// <summary>
    /// Stable source-provided vehicle or scoring identifier.
    /// </summary>
    public string DriverId { get; init; } = string.Empty;

    /// <summary>
    /// Raw world X coordinate in meters.
    /// </summary>
    public double PosX { get; init; }

    /// <summary>
    /// Raw world Y coordinate in meters.
    /// </summary>
    public double PosY { get; init; }

    /// <summary>
    /// Raw world Z coordinate in meters.
    /// </summary>
    public double PosZ { get; init; }
}