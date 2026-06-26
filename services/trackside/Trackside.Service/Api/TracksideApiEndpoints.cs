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
using Trackside.Application.Persistence;
using Trackside.Domain.LiveSession;
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

        endpoints.MapGet(LiveSessionRoutes.BestLapsPath, GetBestLapsAsync)
            .WithName("GetBestLapLeaderboard")
            .WithSummary("Returns daily, weekly, monthly, or all-time best counted laps.");

        endpoints.MapGet(LiveSessionRoutes.MonthlyTrackPath, GetMonthlyTrackAsync)
            .WithName("GetMonthlyTrack")
            .WithSummary("Returns the active monthly track period.");

        endpoints.MapGet(LiveSessionRoutes.LastFinishedSessionPath, GetLastFinishedSessionAsync)
            .WithName("GetLastFinishedSession")
            .WithSummary("Returns the most recently finished session result.");

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

        endpoints.MapGet(LiveSessionRoutes.AdminSessionSetupPath, GetSessionSetupAsync)
            .RequireAuthorization()
            .WithName("GetSessionSetup")
            .WithSummary("Returns prepared rig/name/profile setup.");

        endpoints.MapPut(LiveSessionRoutes.AdminSessionSetupPath, SaveSessionSetupAsync)
            .RequireAuthorization()
            .WithName("SaveSessionSetup")
            .WithSummary("Saves prepared rig/name/profile setup.");

        endpoints.MapDelete(LiveSessionRoutes.AdminSessionSetupPath, ClearSessionSetupAsync)
            .RequireAuthorization()
            .WithName("ClearSessionSetup")
            .WithSummary("Clears prepared rig/name/profile setup.");

        endpoints.MapGet(LiveSessionRoutes.AdminDriverProfilesPath, GetDriverProfilesAsync)
            .RequireAuthorization()
            .WithName("GetDriverProfiles")
            .WithSummary("Returns optional recurring-customer driver profiles.");

        endpoints.MapPost(LiveSessionRoutes.AdminDriverProfilesPath, CreateDriverProfileAsync)
            .RequireAuthorization()
            .WithName("CreateDriverProfile")
            .WithSummary("Creates a recurring-customer driver profile.");

        endpoints.MapPut(LiveSessionRoutes.AdminMonthlyTrackPath, SetMonthlyTrackAsync)
            .RequireAuthorization()
            .WithName("SetMonthlyTrack")
            .WithSummary("Starts a fresh monthly track period.");

        endpoints.MapPost($"{LiveSessionRoutes.AdminMonthlyTrackPath}/reset", ResetMonthlyTrackAsync)
            .RequireAuthorization()
            .WithName("ResetMonthlyTrack")
            .WithSummary("Starts a fresh period for the current monthly track.");

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

    private static async Task<IResult> GetBestLapsAsync(
        string? window,
        string? mode,
        string? trackName,
        int? limit,
        ITracksideStore store,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var normalizedWindow = NormalizeBestLapWindow(window);
        if (normalizedWindow is null)
        {
            return Results.BadRequest(new { error = "Window must be daily, weekly, monthly, or all." });
        }

        var boardMode = ParseBestLapBoardMode(mode);
        if (boardMode is null)
        {
            return Results.BadRequest(new { error = "Mode must be per-driver or all-laps." });
        }

        var nowUtc = timeProvider.GetUtcNow();
        var monthlyTrack = await store.GetActiveMonthlyTrackAsync(cancellationToken);
        var bounds = BuildBestLapWindow(normalizedWindow, trackName, monthlyTrack, nowUtc);
        var rows = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            FromUtc = bounds.FromUtc,
            ToUtc = bounds.ToUtc,
            TrackName = bounds.TrackName,
            Mode = boardMode.Value,
            Limit = limit ?? 20,
            SortByTrack = string.IsNullOrWhiteSpace(bounds.TrackName),
        }, cancellationToken);

        return Results.Ok(new BestLapBoardResponse
        {
            Window = normalizedWindow,
            Mode = ToBestLapBoardModeValue(boardMode.Value),
            TrackName = bounds.TrackName,
            FromUtc = bounds.FromUtc,
            ToUtc = bounds.ToUtc,
            MonthlyTrack = monthlyTrack is null ? null : MonthlyTrackResponse.From(monthlyTrack),
            Rows = rows.Select((row, index) => BestLapRowResponse.From(row, index + 1)).ToList(),
        });
    }

    private static async Task<IResult> GetMonthlyTrackAsync(ITracksideStore store, CancellationToken cancellationToken)
    {
        var period = await store.GetActiveMonthlyTrackAsync(cancellationToken);
        return Results.Ok(period is null ? MonthlyTrackResponse.Empty : MonthlyTrackResponse.From(period));
    }

    private static async Task<IResult> GetLastFinishedSessionAsync(ITracksideStore store, CancellationToken cancellationToken)
    {
        var result = await store.GetLastFinishedSessionResultAsync(cancellationToken);
        return Results.Ok(result is null ? LastFinishedSessionResponse.Empty : LastFinishedSessionResponse.From(result));
    }

    private static async Task<IResult> GetSessionSetupAsync(ITracksideStore store, CancellationToken cancellationToken)
    {
        var entries = await store.GetPreparedSessionEntriesAsync(cancellationToken);
        var profiles = await store.GetDriverProfilesAsync(cancellationToken);
        var isConfigured = await store.IsPreparedSessionSetupConfiguredAsync(cancellationToken);
        return Results.Ok(new SessionSetupResponse
        {
            IsConfigured = isConfigured,
            Entries = entries.Select(SessionSetupEntryResponse.From).ToList(),
            DriverProfiles = profiles.Select(DriverProfileResponse.From).ToList(),
        });
    }

    private static async Task<IResult> SaveSessionSetupAsync(
        SessionSetupRequest request,
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        try
        {
            await store.SavePreparedSessionEntriesAsync(request.Entries, cancellationToken);
            return await GetSessionSetupAsync(store, cancellationToken);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ClearSessionSetupAsync(ITracksideStore store, CancellationToken cancellationToken)
    {
        await store.ClearPreparedSessionEntriesAsync(cancellationToken);
        return await GetSessionSetupAsync(store, cancellationToken);
    }

    private static async Task<IResult> GetDriverProfilesAsync(ITracksideStore store, CancellationToken cancellationToken)
    {
        var profiles = await store.GetDriverProfilesAsync(cancellationToken);
        return Results.Ok(profiles.Select(DriverProfileResponse.From).ToList());
    }

    private static async Task<IResult> CreateDriverProfileAsync(
        DriverProfileRequest request,
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(DriverProfileResponse.From(await store.CreateDriverProfileAsync(request, cancellationToken)));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> SetMonthlyTrackAsync(
        MonthlyTrackRequest request,
        ITracksideStore store,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            var period = await store.StartMonthlyTrackAsync(request.TrackName, timeProvider.GetUtcNow(), request.Reason, cancellationToken);
            return Results.Ok(MonthlyTrackResponse.From(period));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static async Task<IResult> ResetMonthlyTrackAsync(
        ResetMonthlyTrackRequest request,
        ITracksideStore store,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var current = await store.GetActiveMonthlyTrackAsync(cancellationToken);
        if (current is null)
        {
            return Results.BadRequest(new { error = "No active monthly track has been set." });
        }

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Monthly track reset" : request.Reason;
        var period = await store.StartMonthlyTrackAsync(current.TrackName, timeProvider.GetUtcNow(), reason, cancellationToken);
        return Results.Ok(MonthlyTrackResponse.From(period));
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
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        var health = GetHealthPayload(options.Value, state, timeProvider, runtimeContext, includeSensitivePaths: true);
        var adminStatus = new AdminStatusResponse
        {
            Health = health,
            AdminUserStorePath = userStore.StorePath,
            AdminUserCount = (await userStore.GetUsersAsync(cancellationToken)).Count,
            Persistence = store.Status,
            ContentRootPath = runtimeContext.ContentRootPath,
            ExternalConfigRoot = runtimeContext.ExternalConfigRoot,
        };

        return Results.Ok(adminStatus);
    }

    private static async Task<IResult> GetSourceConfiguration(
        IOptionsMonitor<TracksideOptions> options,
        TracksideSourceConfigurationStore configurationStore,
        ITracksideStore store,
        IRf2SharedMemoryMapDiscovery mapDiscovery,
        Rf2ScoringMapResolver mapResolver,
        CancellationToken cancellationToken)
    {
        var source = options.CurrentValue.Source;
        var aliases = await GetDriverAliasesAsync(source, store, cancellationToken);
        var discovery = BuildDiscoveryResponse(source.SharedMemory, mapDiscovery, mapResolver);

        return Results.Ok(new SourceConfigurationResponse
        {
            Mode = source.Mode,
            FixturePath = source.FixturePath,
            DriverAliases = new Dictionary<string, string>(aliases, StringComparer.OrdinalIgnoreCase),
            SharedMemory = source.SharedMemory,
            WritableConfigurationPath = configurationStore.WritableConfigurationPath,
            Discovery = discovery,
        });
    }

    private static async Task<IResult> SaveSourceConfigurationAsync(
        SourceConfigurationRequest request,
        TracksideSourceConfigurationStore configurationStore,
        ITracksideStore store,
        IConfiguration configuration,
        IRf2SharedMemoryMapDiscovery mapDiscovery,
        Rf2ScoringMapResolver mapResolver,
        CancellationToken cancellationToken)
    {
        try
        {
            await configurationStore.SaveAsync(request, cancellationToken, includeDriverAliases: !store.IsEnabled);
            await store.SaveDriverAliasesAsync(request.DriverAliases, cancellationToken);
            if (configuration is IConfigurationRoot configurationRoot)
            {
                configurationRoot.Reload();
            }

            var aliases = store.IsEnabled
                ? await store.GetDriverAliasesAsync(cancellationToken)
                : request.DriverAliases;

            var source = new TracksideSourceOptions
            {
                Mode = request.Mode,
                FixturePath = request.FixturePath,
                DriverAliases = new Dictionary<string, string>(aliases, StringComparer.OrdinalIgnoreCase),
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

    private static async Task<IReadOnlyDictionary<string, string>> GetDriverAliasesAsync(
        TracksideSourceOptions source,
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        if (!store.IsEnabled)
        {
            return source.DriverAliases;
        }

        await store.SeedDriverAliasesAsync(source.DriverAliases, cancellationToken);
        return await store.GetDriverAliasesAsync(cancellationToken);
    }

    private static string? NormalizeBestLapWindow(string? window)
    {
        var normalized = string.IsNullOrWhiteSpace(window) ? "monthly" : window.Trim().ToLowerInvariant();
        return normalized is "daily" or "weekly" or "monthly" or "all" ? normalized : null;
    }

    private static BestLapBoardMode? ParseBestLapBoardMode(string? mode)
    {
        var normalized = string.IsNullOrWhiteSpace(mode) ? "per-driver" : mode.Trim().ToLowerInvariant();
        return normalized switch
        {
            "per-driver" => BestLapBoardMode.PerDriver,
            "all-laps" => BestLapBoardMode.AllLaps,
            _ => null,
        };
    }

    private static string ToBestLapBoardModeValue(BestLapBoardMode mode) => mode == BestLapBoardMode.AllLaps ? "all-laps" : "per-driver";

    private static BestLapWindowBounds BuildBestLapWindow(
        string window,
        string? trackName,
        MonthlyTrackPeriod? monthlyTrack,
        DateTimeOffset nowUtc)
    {
        var trimmedTrackName = string.IsNullOrWhiteSpace(trackName) ? null : trackName.Trim();
        return window switch
        {
            "daily" => new BestLapWindowBounds(StartOfLocalDay(nowUtc), nowUtc, trimmedTrackName),
            "weekly" => new BestLapWindowBounds(StartOfLocalWeek(nowUtc), nowUtc, trimmedTrackName),
            "monthly" when trimmedTrackName is null && monthlyTrack is not null => new BestLapWindowBounds(monthlyTrack.StartedUtc, nowUtc, monthlyTrack.TrackName),
            "monthly" => new BestLapWindowBounds(StartOfLocalMonth(nowUtc), nowUtc, trimmedTrackName),
            _ => new BestLapWindowBounds(null, null, trimmedTrackName),
        };
    }

    private static DateTimeOffset StartOfLocalDay(DateTimeOffset nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, TimeZoneInfo.Local);
        return new DateTimeOffset(localNow.Date, localNow.Offset).ToUniversalTime();
    }

    private static DateTimeOffset StartOfLocalWeek(DateTimeOffset nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, TimeZoneInfo.Local);
        var daysSinceMonday = ((int)localNow.DayOfWeek + 6) % 7;
        var monday = localNow.Date.AddDays(-daysSinceMonday);
        return new DateTimeOffset(monday, localNow.Offset).ToUniversalTime();
    }

    private static DateTimeOffset StartOfLocalMonth(DateTimeOffset nowUtc)
    {
        var localNow = TimeZoneInfo.ConvertTime(nowUtc, TimeZoneInfo.Local);
        var firstOfMonth = new DateTime(localNow.Year, localNow.Month, 1);
        return new DateTimeOffset(firstOfMonth, localNow.Offset).ToUniversalTime();
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
/// Prepared session setup response.
/// </summary>
public sealed record SessionSetupResponse
{
    /// <summary>
    /// True after staff has explicitly saved or cleared prepared session setup.
    /// </summary>
    public bool IsConfigured { get; init; }

    /// <summary>
    /// Prepared rig assignments.
    /// </summary>
    public IReadOnlyList<SessionSetupEntryResponse> Entries { get; init; } = [];

    /// <summary>
    /// Available optional driver profiles.
    /// </summary>
    public IReadOnlyList<DriverProfileResponse> DriverProfiles { get; init; } = [];
}

/// <summary>
/// Prepared session setup save request.
/// </summary>
public sealed record SessionSetupRequest
{
    /// <summary>
    /// Prepared rig assignments.
    /// </summary>
    public List<PreparedSessionEntryRequest> Entries { get; init; } = [];
}

/// <summary>
/// Prepared rig assignment response.
/// </summary>
public sealed record SessionSetupEntryResponse
{
    /// <summary>
    /// Fixed rig or rFactor 2 name.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Screen name assigned to the rig.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional recurring-customer profile id.
    /// </summary>
    public string? DriverProfileId { get; init; }

    /// <summary>
    /// Optional recurring-customer profile display name.
    /// </summary>
    public string? DriverProfileDisplayName { get; init; }

    /// <summary>
    /// Maps a store entry to API response.
    /// </summary>
    /// <param name="entry">Store entry.</param>
    /// <returns>API response.</returns>
    public static SessionSetupEntryResponse From(PreparedSessionEntry entry) => new()
    {
        RigName = entry.RigName,
        DisplayName = entry.DisplayName,
        DriverProfileId = entry.DriverProfileId,
        DriverProfileDisplayName = entry.DriverProfileDisplayName,
    };
}

/// <summary>
/// Driver profile API response.
/// </summary>
public sealed record DriverProfileResponse
{
    /// <summary>
    /// Durable profile id.
    /// </summary>
    public string DriverProfileId { get; init; } = string.Empty;

    /// <summary>
    /// Staff-facing profile display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional email for future result/report delivery.
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// Optional staff notes.
    /// </summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Maps a store profile to API response.
    /// </summary>
    /// <param name="profile">Store profile.</param>
    /// <returns>API response.</returns>
    public static DriverProfileResponse From(DriverProfile profile) => new()
    {
        DriverProfileId = profile.DriverProfileId,
        DisplayName = profile.DisplayName,
        Email = profile.Email,
        Notes = profile.Notes,
    };
}

/// <summary>
/// Request to start a fresh monthly track period.
/// </summary>
public sealed record MonthlyTrackRequest
{
    /// <summary>
    /// Track name to make active for the monthly board.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Optional admin reason for the period change.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Request to reset the current monthly track period.
/// </summary>
public sealed record ResetMonthlyTrackRequest
{
    /// <summary>
    /// Optional admin reason for the reset.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Public best-lap board payload.
/// </summary>
public sealed record BestLapBoardResponse
{
    /// <summary>
    /// Requested board window: daily, weekly, monthly, or all.
    /// </summary>
    public string Window { get; init; } = "monthly";

    /// <summary>
    /// Ranking mode: per-driver or all-laps.
    /// </summary>
    public string Mode { get; init; } = "per-driver";

    /// <summary>
    /// Track filter used by the board, when any.
    /// </summary>
    public string? TrackName { get; init; }

    /// <summary>
    /// Inclusive lower UTC bound used by the query.
    /// </summary>
    public DateTimeOffset? FromUtc { get; init; }

    /// <summary>
    /// Exclusive upper UTC bound used by the query.
    /// </summary>
    public DateTimeOffset? ToUtc { get; init; }

    /// <summary>
    /// Active monthly track period, when configured.
    /// </summary>
    public MonthlyTrackResponse? MonthlyTrack { get; init; }

    /// <summary>
    /// Best counted timed laps ordered by lap time.
    /// </summary>
    public IReadOnlyList<BestLapRowResponse> Rows { get; init; } = [];
}

/// <summary>
/// Public best-lap row payload.
/// </summary>
public sealed record BestLapRowResponse
{
    /// <summary>
    /// One-based row rank.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Track name associated with the lap.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Driver display name captured for the lap.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Underlying rig name.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Vehicle name captured for the lap.
    /// </summary>
    public string VehicleName { get; init; } = string.Empty;

    /// <summary>
    /// Completed lap number.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Best counted lap time in seconds.
    /// </summary>
    public double LapSeconds { get; init; }

    /// <summary>
    /// UTC timestamp when the lap was observed.
    /// </summary>
    public DateTimeOffset ObservedUtc { get; init; }

    /// <summary>
    /// Maps a store row to a public response with caller-assigned rank.
    /// </summary>
    /// <param name="row">Store row.</param>
    /// <param name="rank">One-based rank.</param>
    /// <returns>Public response.</returns>
    public static BestLapRowResponse From(HistoricalBestLap row, int rank) => new()
    {
        Rank = rank,
        TrackName = row.TrackName,
        DisplayName = row.DisplayName,
        RigName = row.RigName,
        VehicleName = row.VehicleName,
        LapNumber = row.LapNumber,
        LapSeconds = row.BestLapSeconds,
        ObservedUtc = row.ObservedUtc,
    };
}

/// <summary>
/// Active monthly track period response.
/// </summary>
public sealed record MonthlyTrackResponse
{
    /// <summary>
    /// Empty response used before staff set a monthly track.
    /// </summary>
    public static MonthlyTrackResponse Empty { get; } = new();

    /// <summary>
    /// True when a monthly track period is active.
    /// </summary>
    public bool IsActive { get; init; }

    /// <summary>
    /// Active track name.
    /// </summary>
    public string? TrackName { get; init; }

    /// <summary>
    /// UTC timestamp when the active period started.
    /// </summary>
    public DateTimeOffset? StartedUtc { get; init; }

    /// <summary>
    /// Optional admin reason for the active period.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Maps a store period to public response.
    /// </summary>
    /// <param name="period">Store period.</param>
    /// <returns>Public response.</returns>
    public static MonthlyTrackResponse From(MonthlyTrackPeriod period) => new()
    {
        IsActive = true,
        TrackName = period.TrackName,
        StartedUtc = period.StartedUtc,
        Reason = period.Reason,
    };
}

/// <summary>
/// Last finished session response.
/// </summary>
public sealed record LastFinishedSessionResponse
{
    /// <summary>
    /// Empty response used before a finished session has been observed.
    /// </summary>
    public static LastFinishedSessionResponse Empty { get; } = new();

    /// <summary>
    /// True when a finished session result is available.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Track name associated with the session.
    /// </summary>
    public string? TrackName { get; init; }

    /// <summary>
    /// Session kind associated with the result.
    /// </summary>
    public SessionKind? SessionKind { get; init; }

    /// <summary>
    /// UTC timestamp when Trackside last observed the session.
    /// </summary>
    public DateTimeOffset? LastSeenUtc { get; init; }

    /// <summary>
    /// Finished result rows.
    /// </summary>
    public IReadOnlyList<LastFinishedSessionRowResponse> Rows { get; init; } = [];

    /// <summary>
    /// Maps store result to API response.
    /// </summary>
    /// <param name="result">Store result.</param>
    /// <returns>API response.</returns>
    public static LastFinishedSessionResponse From(FinishedSessionResult result) => new()
    {
        IsAvailable = true,
        TrackName = result.TrackName,
        SessionKind = result.SessionKind,
        LastSeenUtc = result.LastSeenUtc,
        Rows = result.Rows.Select(LastFinishedSessionRowResponse.From).ToList(),
    };
}

/// <summary>
/// Last finished session result row response.
/// </summary>
public sealed record LastFinishedSessionRowResponse
{
    /// <summary>
    /// One-based result rank.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Screen name captured for the session.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Fixed rig or rFactor 2 name.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Optional driver profile id.
    /// </summary>
    public string? DriverProfileId { get; init; }

    /// <summary>
    /// Vehicle name.
    /// </summary>
    public string VehicleName { get; init; } = string.Empty;

    /// <summary>
    /// Completed lap count.
    /// </summary>
    public int CompletedLaps { get; init; }

    /// <summary>
    /// Best lap seconds.
    /// </summary>
    public double? BestLapSeconds { get; init; }

    /// <summary>
    /// Maps store row to API response.
    /// </summary>
    /// <param name="row">Store row.</param>
    /// <returns>API response.</returns>
    public static LastFinishedSessionRowResponse From(FinishedSessionResultRow row) => new()
    {
        Rank = row.Rank,
        DisplayName = row.DisplayName,
        RigName = row.RigName,
        DriverProfileId = row.DriverProfileId,
        VehicleName = row.VehicleName,
        CompletedLaps = row.CompletedLaps,
        BestLapSeconds = row.BestLapSeconds,
    };
}

/// <summary>
/// Calculated bounds for a best-lap board.
/// </summary>
/// <param name="FromUtc">Inclusive lower UTC bound.</param>
/// <param name="ToUtc">Exclusive upper UTC bound.</param>
/// <param name="TrackName">Track filter.</param>
internal sealed record BestLapWindowBounds(DateTimeOffset? FromUtc, DateTimeOffset? ToUtc, string? TrackName);

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
    /// Provider-neutral durable persistence status.
    /// </summary>
    public TracksideStoreStatus Persistence { get; init; } = new();

    /// <summary>
    /// Service content root.
    /// </summary>
    public string ContentRootPath { get; init; } = string.Empty;

    /// <summary>
    /// External config root when installed as a bundle/service.
    /// </summary>
    public string? ExternalConfigRoot { get; init; }
}
