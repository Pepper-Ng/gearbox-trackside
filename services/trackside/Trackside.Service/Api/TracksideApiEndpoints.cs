using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Infrastructure.Rf2.SharedMemory;
using Trackside.Service.Configuration;
using Trackside.Service.Hosting;
using Trackside.Service.Security;

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

        endpoints.MapGet(LiveSessionRoutes.AdminBootstrapPath, GetAdminBootstrapAsync)
            .WithName("GetAdminBootstrap")
            .WithSummary("Returns whether first-admin setup is required.");

        endpoints.MapPost(LiveSessionRoutes.AdminBootstrapPath, CreateFirstAdminAsync)
            .WithName("CreateFirstAdmin")
            .WithSummary("Creates the first admin user when no admins exist.");

        endpoints.MapGet(LiveSessionRoutes.AdminSessionPath, GetAdminSessionAsync)
            .WithName("GetAdminSession")
            .WithSummary("Returns current admin authentication state.");

        endpoints.MapPost(LiveSessionRoutes.AdminSessionPath, LoginAdminAsync)
            .WithName("LoginAdmin")
            .WithSummary("Authenticates an admin and creates an admin cookie session.");

        endpoints.MapDelete(LiveSessionRoutes.AdminSessionPath, async (HttpContext httpContext) => await LogoutAdminAsync(httpContext))
            .WithName("LogoutAdmin")
            .WithSummary("Clears the admin cookie session.");

        endpoints.MapGet(LiveSessionRoutes.AdminUsersPath, GetAdminUsersAsync)
            .RequireAuthorization()
            .WithName("GetAdminUsers")
            .WithSummary("Returns admin user summaries.");

        endpoints.MapPost(LiveSessionRoutes.AdminUsersPath, CreateAdminUserAsync)
            .RequireAuthorization()
            .WithName("CreateAdminUser")
            .WithSummary("Creates another admin user.");

        endpoints.MapPut($"{LiveSessionRoutes.AdminUsersPath}/{{username}}/password", ChangeAdminPasswordAsync)
            .RequireAuthorization()
            .WithName("ChangeAdminPassword")
            .WithSummary("Changes an admin user's password.");

        endpoints.MapGet(LiveSessionRoutes.AdminStatusPath, GetAdminStatusAsync)
            .RequireAuthorization()
            .WithName("GetAdminStatus")
            .WithSummary("Returns admin-only status and path details.");

        endpoints.MapGet(LiveSessionRoutes.SourceConfigurationPath, GetSourceConfiguration)
            .RequireAuthorization()
            .WithName("GetSourceConfiguration")
            .WithSummary("Returns editable source and shared-memory discovery configuration.");

        endpoints.MapPut(LiveSessionRoutes.SourceConfigurationPath, SaveSourceConfigurationAsync)
            .RequireAuthorization()
            .WithName("SaveSourceConfiguration")
            .WithSummary("Persists editable source and shared-memory discovery configuration.");

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

    private static async Task<IResult> GetAdminBootstrapAsync(AdminUserStore userStore, CancellationToken cancellationToken)
    {
        return Results.Ok(new AdminBootstrapResponse(!await userStore.HasUsersAsync(cancellationToken)));
    }

    private static async Task<IResult> CreateFirstAdminAsync(
        AdminCreateUserRequest request,
        AdminUserStore userStore,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        try
        {
            var user = await userStore.CreateFirstAdminAsync(request, cancellationToken);
            await SignInAsync(httpContext, user);
            return Results.Ok(new AdminSessionResponse(true, user.Username, user.DisplayName, BootstrapRequired: false));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetAdminSessionAsync(HttpContext httpContext, AdminUserStore userStore, CancellationToken cancellationToken)
    {
        var bootstrapRequired = !await userStore.HasUsersAsync(cancellationToken);
        var username = httpContext.User.Identity?.IsAuthenticated == true
            ? httpContext.User.Identity.Name
            : null;
        var displayName = httpContext.User.FindFirstValue("display_name");
        return Results.Ok(new AdminSessionResponse(username is not null, username, displayName, bootstrapRequired));
    }

    private static async Task<IResult> LoginAdminAsync(
        AdminLoginRequest request,
        AdminUserStore userStore,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var user = await userStore.VerifyAsync(request.Username, request.Password, cancellationToken);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        await SignInAsync(httpContext, user);
        return Results.Ok(new AdminSessionResponse(true, user.Username, user.DisplayName, BootstrapRequired: false));
    }

    private static async Task<IResult> LogoutAdminAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok(new AdminSessionResponse(false, null, null, BootstrapRequired: false));
    }

    private static async Task<IResult> GetAdminUsersAsync(AdminUserStore userStore, CancellationToken cancellationToken)
    {
        return Results.Ok(await userStore.GetUsersAsync(cancellationToken));
    }

    private static async Task<IResult> CreateAdminUserAsync(
        AdminCreateUserRequest request,
        AdminUserStore userStore,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await userStore.CreateUserAsync(request, cancellationToken));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ChangeAdminPasswordAsync(
        string username,
        AdminChangePasswordRequest request,
        AdminUserStore userStore,
        CancellationToken cancellationToken)
    {
        try
        {
            await userStore.ChangePasswordAsync(username, request.NewPassword, cancellationToken);
            return Results.Ok(new { status = "password changed" });
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> GetAdminStatusAsync(
        IOptions<TracksideOptions> options,
        LiveSessionState state,
        TimeProvider timeProvider,
        TracksideRuntimeContext runtimeContext,
        AdminUserStore userStore,
        CancellationToken cancellationToken)
    {
        var health = GetHealthPayload(options.Value, state, timeProvider, runtimeContext, includeSensitivePaths: true);
        var adminStatus = new AdminStatusResponse
        {
            Health = health,
            AdminUserStorePath = userStore.StorePath,
            AdminUserCount = (await userStore.GetUsersAsync(cancellationToken)).Count,
            ContentRootPath = runtimeContext.ContentRootPath,
            ExternalConfigRoot = runtimeContext.ExternalConfigRoot,
        };

        return Results.Ok(adminStatus);
    }

    private static IResult GetSourceConfiguration(
        IOptionsMonitor<TracksideOptions> options,
        TracksideSourceConfigurationStore configurationStore,
        IRf2SharedMemoryMapDiscovery mapDiscovery,
        Rf2ScoringMapResolver mapResolver)
    {
        var source = options.CurrentValue.Source;
        var discovery = BuildDiscoveryResponse(source.SharedMemory, mapDiscovery, mapResolver);

        return Results.Ok(new SourceConfigurationResponse
        {
            Mode = source.Mode,
            FixturePath = source.FixturePath,
            DriverAliases = source.DriverAliases,
            SharedMemory = source.SharedMemory,
            WritableConfigurationPath = configurationStore.WritableConfigurationPath,
            Discovery = discovery,
        });
    }

    private static async Task<IResult> SaveSourceConfigurationAsync(
        SourceConfigurationRequest request,
        TracksideSourceConfigurationStore configurationStore,
        IConfiguration configuration,
        IRf2SharedMemoryMapDiscovery mapDiscovery,
        Rf2ScoringMapResolver mapResolver,
        CancellationToken cancellationToken)
    {
        try
        {
            await configurationStore.SaveAsync(request, cancellationToken);
            if (configuration is IConfigurationRoot configurationRoot)
            {
                configurationRoot.Reload();
            }

            var source = new TracksideSourceOptions
            {
                Mode = request.Mode,
                FixturePath = request.FixturePath,
                DriverAliases = request.DriverAliases,
                SharedMemory = request.SharedMemory,
            };

            return Results.Ok(new SourceConfigurationResponse
            {
                Mode = source.Mode,
                FixturePath = source.FixturePath,
                DriverAliases = source.DriverAliases,
                SharedMemory = source.SharedMemory,
                WritableConfigurationPath = configurationStore.WritableConfigurationPath,
                Discovery = BuildDiscoveryResponse(source.SharedMemory, mapDiscovery, mapResolver),
            });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IResult GetHealth(
        IOptions<TracksideOptions> options,
        LiveSessionState state,
        TimeProvider timeProvider,
        TracksideRuntimeContext runtimeContext)
    {
        var health = GetHealthPayload(options.Value, state, timeProvider, runtimeContext, includeSensitivePaths: false);
        return Results.Ok(new TracksidePublicHealthResponse
        {
            Status = health.Status,
            TimestampUtc = health.TimestampUtc,
            Version = health.Version,
            AppVersion = health.AppVersion,
            InstallMode = health.InstallMode,
            ServiceState = health.ServiceState,
            CurrentSessionAvailable = health.CurrentSessionAvailable,
        });
    }

    private static TracksideHealthResponse GetHealthPayload(
        TracksideOptions options,
        LiveSessionState state,
        TimeProvider timeProvider,
        TracksideRuntimeContext runtimeContext,
        bool includeSensitivePaths)
    {
        var current = state.Current;
        var assembly = Assembly.GetExecutingAssembly();
        var appVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "0.0.0";
        var deployment = options.Deployment;
        var updates = options.Updates;
        return new TracksideHealthResponse
        {
            TimestampUtc = timeProvider.GetUtcNow(),
            Version = assembly.GetName().Version?.ToString() ?? "0.0.0",
            AppVersion = appVersion,
            BundleVersion = deployment.BundleVersion ?? appVersion,
            SourceMode = options.Source.Mode,
            PublicBaseUrl = options.Http.PublicBaseUrl,
            InstallMode = deployment.InstallMode,
            ServiceState = runtimeContext.ServiceState,
            InstallRoot = includeSensitivePaths ? deployment.InstallRoot : null,
            ConfigPath = includeSensitivePaths ? deployment.ConfigPath ?? runtimeContext.ExternalConfigRoot : null,
            DataPath = includeSensitivePaths ? deployment.DataPath : null,
            LogsPath = includeSensitivePaths ? deployment.LogsPath : null,
            UpdatesPath = includeSensitivePaths ? deployment.UpdatesPath : null,
            ManifestPath = includeSensitivePaths ? deployment.ManifestPath : null,
            CurrentSessionAvailable = current is not null,
            CurrentTrackName = current?.Session.TrackName,
            CurrentSourceStatus = current?.Status,
            Update = new TracksideUpdateHealthResponse
            {
                Status = updates.Status,
                Channel = updates.Channel,
                ManifestUrlConfigured = !string.IsNullOrWhiteSpace(updates.ManifestUrl),
                ManifestUrl = updates.ManifestUrl,
                CandidateManifestPath = includeSensitivePaths ? updates.CandidateManifestPath : null,
                MinimumCompatibleVersion = updates.MinimumCompatibleVersion,
            },
        };
    }

    private static async Task SignInAsync(HttpContext httpContext, AdminUserSummary user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.Username),
            new("display_name", user.DisplayName),
            new(ClaimTypes.Role, "Admin"),
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
    }

    private static SharedMemoryDiscoveryResponse BuildDiscoveryResponse(
        TracksideSharedMemoryOptions sharedMemoryOptions,
        IRf2SharedMemoryMapDiscovery mapDiscovery,
        Rf2ScoringMapResolver mapResolver)
    {
        var discoveredMaps = sharedMemoryOptions.AutoDiscover
            ? mapDiscovery.DiscoverScoringMaps(sharedMemoryOptions)
            : [];
        var resolution = mapResolver.Resolve(sharedMemoryOptions, discoveredMaps);

        return new SharedMemoryDiscoveryResponse
        {
            IsAmbiguous = resolution.IsAmbiguous,
            Status = resolution.Status,
            CandidateMapNames = resolution.CandidateMapNames,
            DiscoveredCandidates = discoveredMaps.Select(ToResponse).ToList(),
            AmbiguousCandidates = resolution.AmbiguousCandidates.Select(ToResponse).ToList(),
        };
    }

    private static SharedMemoryMapCandidateResponse ToResponse(Rf2ScoringMapCandidate candidate) => new()
    {
        MapName = candidate.MapName,
        ProcessId = candidate.ProcessId,
        ProcessName = candidate.ProcessName,
        DiscoverySource = candidate.DiscoverySource,
    };
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
/// Public health payload returned by <c>/api/health</c> without advanced paths or source diagnostics.
/// </summary>
public sealed record TracksidePublicHealthResponse
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
    /// Logical install mode such as Development, Service, or BundleSmoke.
    /// </summary>
    public string InstallMode { get; init; } = TracksideDeploymentOptions.DefaultInstallMode;

    /// <summary>
    /// Runtime state of the service process.
    /// </summary>
    public string ServiceState { get; init; } = "Unknown";

    /// <summary>
    /// True after the first snapshot has been loaded successfully.
    /// </summary>
    public bool CurrentSessionAvailable { get; init; }
}

/// <summary>
/// Update-related health payload returned to admin status callers.
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

/// <summary>
/// Public bootstrap state for the admin setup page.
/// </summary>
/// <param name="BootstrapRequired">True when no admin user exists yet.</param>
public sealed record AdminBootstrapResponse(bool BootstrapRequired);

/// <summary>
/// Current admin session state.
/// </summary>
/// <param name="IsAuthenticated">True when the request has an admin cookie.</param>
/// <param name="Username">Authenticated admin username.</param>
/// <param name="DisplayName">Authenticated admin display name.</param>
/// <param name="BootstrapRequired">True when no admin user exists yet.</param>
public sealed record AdminSessionResponse(bool IsAuthenticated, string? Username, string? DisplayName, bool BootstrapRequired);

/// <summary>
/// Admin-only service status payload.
/// </summary>
public sealed record AdminStatusResponse
{
    /// <summary>
    /// Existing health payload with service, install, update, and source details.
    /// </summary>
    public TracksideHealthResponse Health { get; init; } = new();

    /// <summary>
    /// Path to the admin user store.
    /// </summary>
    public string AdminUserStorePath { get; init; } = string.Empty;

    /// <summary>
    /// Number of configured admin users.
    /// </summary>
    public int AdminUserCount { get; init; }

    /// <summary>
    /// Service content root.
    /// </summary>
    public string ContentRootPath { get; init; } = string.Empty;

    /// <summary>
    /// External config root when installed as a bundle/service.
    /// </summary>
    public string? ExternalConfigRoot { get; init; }
}
