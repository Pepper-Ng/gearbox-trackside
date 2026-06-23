using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Trackside.Application.LiveSession;
using Trackside.Host.Configuration;

namespace Trackside.Host.Api;

/// <summary>
/// Maps HTTP endpoints used by local kiosk, admin, diagnostics, and tray actions.
/// </summary>
public static class TracksideApiEndpoints
{
    /// <summary>
    /// Adds Phase 0B Trackside endpoints to the route table.
    /// </summary>
    /// <param name="endpoints">Endpoint builder receiving Trackside routes.</param>
    /// <returns>The same endpoint builder for call chaining.</returns>
    public static IEndpointRouteBuilder MapTracksideApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(LiveSessionRoutes.CurrentSessionPath, GetCurrentSessionAsync)
            .WithName("GetCurrentLiveSession")
            .WithSummary("Returns the current normalized live-session snapshot.");

        endpoints.MapGet(LiveSessionRoutes.ClientConfigurationPath, () => Results.Ok(new ClientConfigurationResponse()))
            .WithName("GetClientConfiguration")
            .WithSummary("Returns stable frontend endpoint paths.");

        endpoints.MapGet(LiveSessionRoutes.HealthPath, GetHealth)
            .WithName("GetTracksideHealth")
            .WithSummary("Returns host health, configuration, and source status.");

        return endpoints;
    }

    private static async Task<IResult> GetCurrentSessionAsync(
        ILiveSessionSource source,
        LiveSessionState state,
        CancellationToken cancellationToken)
    {
        var snapshot = state.Current;
        if (snapshot is null)
        {
            snapshot = await source.GetCurrentAsync(cancellationToken);
            state.Update(snapshot);
        }

        return Results.Ok(snapshot);
    }

    private static IResult GetHealth(IOptions<TracksideOptions> options, LiveSessionState state, TimeProvider timeProvider)
    {
        var current = state.Current;
        var health = new TracksideHealthResponse
        {
            TimestampUtc = timeProvider.GetUtcNow(),
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0",
            SourceMode = options.Value.Source.Mode,
            TrayEnabled = options.Value.Tray.Enabled,
            PublicBaseUrl = options.Value.Http.PublicBaseUrl,
            CurrentSessionAvailable = current is not null,
            CurrentTrackName = current?.Session.TrackName,
            CurrentSourceStatus = current?.Status,
        };

        return Results.Ok(health);
    }
}

/// <summary>
/// Health payload returned by <c>/api/health</c>.
/// </summary>
public sealed record TracksideHealthResponse
{
    /// <summary>
    /// Coarse host health status.
    /// </summary>
    public string Status { get; init; } = "ok";

    /// <summary>
    /// UTC timestamp when the health response was produced.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Host assembly version.
    /// </summary>
    public string Version { get; init; } = "0.0.0";

    /// <summary>
    /// Configured live-session source mode.
    /// </summary>
    public LiveSessionSourceMode SourceMode { get; init; }

    /// <summary>
    /// Whether the Windows tray shell is enabled by configuration.
    /// </summary>
    public bool TrayEnabled { get; init; }

    /// <summary>
    /// Public local base URL opened by tray actions.
    /// </summary>
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// True after the first snapshot has been loaded successfully.
    /// </summary>
    public bool CurrentSessionAvailable { get; init; }

    /// <summary>
    /// Current track name, when a snapshot is available.
    /// </summary>
    public string? CurrentTrackName { get; init; }

    /// <summary>
    /// Current source status, when a snapshot is available.
    /// </summary>
    public string? CurrentSourceStatus { get; init; }
}