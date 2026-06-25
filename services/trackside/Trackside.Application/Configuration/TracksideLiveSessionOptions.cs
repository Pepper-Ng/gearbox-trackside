using System.ComponentModel.DataAnnotations;

namespace Trackside.Application.Configuration;

/// <summary>
/// Cadence settings for publishing normalized session snapshots to browsers.
/// </summary>
public sealed class TracksideLiveSessionOptions
{
    /// <summary>
    /// Minimum supported publish cadence in seconds.
    /// </summary>
    public const double MinimumPublishIntervalSeconds = 0.25;

    /// <summary>
    /// Interval between fixture/API refreshes and SignalR broadcasts.
    /// </summary>
    [Range(MinimumPublishIntervalSeconds, 60.0)]
    public double PublishIntervalSeconds { get; init; } = 1.0;
}