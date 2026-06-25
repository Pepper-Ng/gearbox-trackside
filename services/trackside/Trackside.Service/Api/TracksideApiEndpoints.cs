using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Trackside.Application.LiveSession;
using Trackside.Service.Configuration;
using Trackside.Service.Hosting;

namespace Trackside.Service.Api;

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

    private static IResult GetHealth(
        IOptions<TracksideOptions> options,
        LiveSessionState state,
        TimeProvider timeProvider,
        TracksideRuntimeContext runtimeContext)
    {
        var current = state.Current;
        var assembly = Assembly.GetExecutingAssembly();
        var appVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        var deployment = options.Value.Deployment;
        var updates = options.Value.Updates;
        var health = new TracksideHealthResponse
        {
            TimestampUtc = timeProvider.GetUtcNow(),
            Version = assembly.GetName().Version?.ToString() ?? "0.0.0",
            AppVersion = appVersion,
            BundleVersion = deployment.BundleVersion ?? appVersion,
            SourceMode = options.Value.Source.Mode,
            PublicBaseUrl = options.Value.Http.PublicBaseUrl,
            InstallMode = deployment.InstallMode,
            ServiceState = runtimeContext.ServiceState,
            InstallRoot = deployment.InstallRoot,
            ConfigPath = deployment.ConfigPath ?? runtimeContext.ExternalConfigRoot,
            DataPath = deployment.DataPath,
            LogsPath = deployment.LogsPath,
            UpdatesPath = deployment.UpdatesPath,
            ManifestPath = deployment.ManifestPath,
            CurrentSessionAvailable = current is not null,
            CurrentTrackName = current?.Session.TrackName,
            CurrentSourceStatus = current?.Status,
            Update = new TracksideUpdateHealthResponse
            {
                Status = updates.Status,
                Channel = updates.Channel,
                ManifestUrlConfigured = !string.IsNullOrWhiteSpace(updates.ManifestUrl),
                ManifestUrl = updates.ManifestUrl,
                CandidateManifestPath = updates.CandidateManifestPath,
                MinimumCompatibleVersion = updates.MinimumCompatibleVersion,
            },
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
    /// Application version, normally the assembly informational version.
    /// </summary>
    public string AppVersion { get; init; } = "0.0.0";

    /// <summary>
    /// Versioned bundle identifier produced by the package script.
    /// </summary>
    public string BundleVersion { get; init; } = "0.0.0";

    /// <summary>
    /// Configured live-session source mode.
    /// </summary>
    public LiveSessionSourceMode SourceMode { get; init; }

    /// <summary>
    /// Public local base URL opened by tray actions.
    /// </summary>
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Logical install mode such as Development, Service, or BundleSmoke.
    /// </summary>
    public string InstallMode { get; init; } = TracksideDeploymentOptions.DefaultInstallMode;

    /// <summary>
    /// Runtime state of the service process.
    /// </summary>
    public string ServiceState { get; init; } = "Unknown";

    /// <summary>
    /// Root folder of the installed Trackside bundle, when installed.
    /// </summary>
    public string? InstallRoot { get; init; }

    /// <summary>
    /// External configuration root used by packaged installs.
    /// </summary>
    public string? ConfigPath { get; init; }

    /// <summary>
    /// Writable data path used by packaged installs.
    /// </summary>
    public string? DataPath { get; init; }

    /// <summary>
    /// Writable logs path used by packaged installs.
    /// </summary>
    public string? LogsPath { get; init; }

    /// <summary>
    /// Writable update staging path used by packaged installs.
    /// </summary>
    public string? UpdatesPath { get; init; }

    /// <summary>
    /// Currently installed bundle manifest path, when known.
    /// </summary>
    public string? ManifestPath { get; init; }

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

    /// <summary>
    /// Update-check placeholder state for future dashboard-controlled updates.
    /// </summary>
    public TracksideUpdateHealthResponse Update { get; init; } = new();
}

/// <summary>
/// Update-related health payload returned by <c>/api/health</c>.
/// </summary>
public sealed record TracksideUpdateHealthResponse
{
    /// <summary>
    /// Human-readable update status.
    /// </summary>
    public string Status { get; init; } = TracksideUpdateOptions.DefaultStatus;

    /// <summary>
    /// Update channel name used when remote manifests are introduced.
    /// </summary>
    public string Channel { get; init; } = "local";

    /// <summary>
    /// True when a remote manifest URL has been configured.
    /// </summary>
    public bool ManifestUrlConfigured { get; init; }

    /// <summary>
    /// Remote update manifest URL, when configured.
    /// </summary>
    public string? ManifestUrl { get; init; }

    /// <summary>
    /// Local candidate manifest path used by smoke tests and future staged updates.
    /// </summary>
    public string? CandidateManifestPath { get; init; }

    /// <summary>
    /// Minimum compatible app version advertised by the update channel.
    /// </summary>
    public string MinimumCompatibleVersion { get; init; } = "0.1.0";
}
