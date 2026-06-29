using Microsoft.AspNetCore.SignalR;
using Trackside.Application.LiveSession;
using Trackside.Service.Tracking;

namespace Trackside.Service.Hubs;

/// <summary>
/// Forwards generated track-geometry changes from the live-data bus to SignalR clients.
/// </summary>
public sealed class TrackGeometrySignalRPublisher : ILiveDataConsumer<TrackGeometryChangedFrame>
{
    private readonly IHubContext<LiveSessionHub, ILiveSessionClient> _hubContext;
    private string? _lastGeometryKey;

    /// <summary>
    /// Creates a SignalR geometry publisher.
    /// </summary>
    /// <param name="hubContext">SignalR hub context.</param>
    public TrackGeometrySignalRPublisher(IHubContext<LiveSessionHub, ILiveSessionClient> hubContext)
    {
        _hubContext = hubContext;
    }

    /// <inheritdoc />
    public async ValueTask ConsumeAsync(TrackGeometryChangedFrame frame, CancellationToken cancellationToken)
    {
        var geometry = frame.Geometry;
        var geometryKey = $"{geometry.TrackName}|{geometry.UpdatedUtc:O}|{geometry.SampleCount}|{geometry.CoveragePercent}|{geometry.IsAvailable}|{geometry.IsCompleteLap}";
        if (string.Equals(geometryKey, _lastGeometryKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastGeometryKey = geometryKey;
        await _hubContext.Clients.All.TrackGeometryUpdated(geometry);
    }
}