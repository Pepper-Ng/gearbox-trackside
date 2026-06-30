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
using Trackside.Service.Tracking;

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

        endpoints.MapGet(LiveSessionRoutes.CurrentTrackGeometryPath, GetCurrentTrackGeometryAsync)
            .WithName("GetCurrentTrackGeometry")
            .WithSummary("Returns cached generated track geometry for the current live-session track.");

        endpoints.MapGet(LiveSessionRoutes.ClientConfigurationPath, GetClientConfiguration)
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

        endpoints.MapGet(LiveSessionRoutes.AdminSessionsPath, GetHistoricalSessionsAsync)
            .RequireAuthorization()
            .WithName("GetHistoricalSessions")
            .WithSummary("Returns persisted sessions for staff review.");

        endpoints.MapDelete($"{LiveSessionRoutes.AdminSessionsPath}/empty", DeleteEmptyHistoricalSessionsAsync)
            .RequireAuthorization()
            .WithName("DeleteEmptyHistoricalSessions")
            .WithSummary("Deletes placeholder historical sessions with no participants or known track.");

        endpoints.MapGet($"{LiveSessionRoutes.AdminSessionsPath}/{{sessionId}}", GetHistoricalSessionAsync)
            .RequireAuthorization()
            .WithName("GetHistoricalSession")
            .WithSummary("Returns one persisted session with participant rows.");

        endpoints.MapDelete($"{LiveSessionRoutes.AdminSessionsPath}/{{sessionId}}", DeleteHistoricalSessionAsync)
            .RequireAuthorization()
            .WithName("DeleteHistoricalSession")
            .WithSummary("Deletes a persisted historical session.");

        endpoints.MapPut($"{LiveSessionRoutes.AdminSessionsPath}/{{sessionId}}/history", SetSessionHistoryInclusionAsync)
            .RequireAuthorization()
            .WithName("SetSessionHistoryInclusion")
            .WithSummary("Updates whether a session counts for historical boards.");

        endpoints.MapPut($"{LiveSessionRoutes.AdminSessionsPath}/{{sessionId}}/participants/{{participantId:long}}/correction", CorrectParticipantAsync)
            .RequireAuthorization()
            .WithName("CorrectParticipant")
            .WithSummary("Applies a participant display-name correction or exclusion.");

        endpoints.MapPut($"{LiveSessionRoutes.AdminSessionsPath}/{{sessionId}}/laps/{{lapId:long}}/correction", CorrectLapAsync)
            .RequireAuthorization()
            .WithName("CorrectLap")
            .WithSummary("Applies a lap correction or invalidation.");

        endpoints.MapGet(LiveSessionRoutes.AdminKioskPath, GetKioskSettings)
            .RequireAuthorization()
            .WithName("GetKioskSettings")
            .WithSummary("Returns kiosk display defaults.");

        endpoints.MapPut(LiveSessionRoutes.AdminKioskPath, SaveKioskSettingsAsync)
            .RequireAuthorization()
            .WithName("SaveKioskSettings")
            .WithSummary("Persists kiosk display defaults.");

        endpoints.MapGet(LiveSessionRoutes.AdminDriverTrackerPath, GetDriverTrackerSettings)
            .RequireAuthorization()
            .WithName("GetDriverTrackerSettings")
            .WithSummary("Returns driver tracker settings.");

        endpoints.MapPut(LiveSessionRoutes.AdminDriverTrackerPath, SaveDriverTrackerSettingsAsync)
            .RequireAuthorization()
            .WithName("SaveDriverTrackerSettings")
            .WithSummary("Persists driver tracker settings.");

        endpoints.MapGet(LiveSessionRoutes.AdminDriverTrackerTracksPath, GetDriverTrackerTracks)
            .RequireAuthorization()
            .WithName("GetDriverTrackerTracks")
            .WithSummary("Returns seen tracks and generated-geometry recording status.");

        endpoints.MapPost(LiveSessionRoutes.AdminDriverTrackerRecordingsPath, StartDriverTrackerRecordingAsync)
            .RequireAuthorization()
            .WithName("StartDriverTrackerRecording")
            .WithSummary("Starts or improves generated track geometry from telemetry.");

        endpoints.MapGet(LiveSessionRoutes.AdminLocalizationPath, GetLocalization)
            .RequireAuthorization()
            .WithName("GetLocalization")
            .WithSummary("Returns frontend localization defaults.");

        endpoints.MapPut(LiveSessionRoutes.AdminLocalizationPath, SaveLocalizationAsync)
            .RequireAuthorization()
            .WithName("SaveLocalization")
            .WithSummary("Persists frontend localization defaults.");

        endpoints.MapPost($"{LiveSessionRoutes.AdminPersistencePath}/retention/cleanup", EnforceRetentionAsync)
            .RequireAuthorization()
            .WithName("EnforceRetention")
            .WithSummary("Runs persistence retention cleanup using configured policy.");

        endpoints.MapGet(LiveSessionRoutes.AdminSharedMemoryDebugPath, GetSharedMemoryDebug)
            .RequireAuthorization()
            .WithName("GetSharedMemoryDebug")
            .WithSummary("Returns realtime scoring and telemetry shared-memory diagnostics.");

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
        ILiveDataPublisher liveDataPublisher,
        CancellationToken cancellationToken)
    {
        var snapshot = state.Current;
        if (snapshot is null)
        {
            snapshot = await source.GetCurrentAsync(cancellationToken);
            state.Update(snapshot);
        }

        await liveDataPublisher.PublishAsync(new ScoringContextFrame { Snapshot = snapshot }, cancellationToken);

        return Results.Ok(snapshot);
    }

    private static async Task<IResult> GetCurrentTrackGeometryAsync(
        ILiveSessionSource source,
        LiveSessionState state,
        ILiveDataPublisher liveDataPublisher,
        TrackGeometryRecorder trackGeometryRecorder,
        CancellationToken cancellationToken)
    {
        var snapshot = state.Current;
        if (snapshot is null)
        {
            snapshot = await source.GetCurrentAsync(cancellationToken);
            state.Update(snapshot);
        }

        await liveDataPublisher.PublishAsync(new ScoringContextFrame { Snapshot = snapshot }, cancellationToken);
        return Results.Ok(trackGeometryRecorder.Get(snapshot.Session.TrackName));
    }

    private static IResult GetClientConfiguration(IOptionsMonitor<TracksideOptions> options) => Results.Ok(new ClientConfigurationResponse
    {
        DefaultDisplayMode = options.CurrentValue.Kiosk.DefaultDisplayMode,
        DefaultLanguage = options.CurrentValue.Localization.DefaultLanguage,
        DriverTrackerClientRefreshHz = options.CurrentValue.DriverTracker.ClientRefreshHz,
    });

    private static async Task<IResult> GetLocalization(IOptionsMonitor<TracksideOptions> options) => Results.Ok(LocalizationResponse.From(options.CurrentValue.Localization));

    private static async Task<IResult> SaveLocalizationAsync(
        LocalizationRequest request,
        TracksideWritableConfigurationStore configurationStore,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DefaultLanguage) || (request.DefaultLanguage != "en" && request.DefaultLanguage != "nl"))
        {
            return Results.BadRequest(new { error = "DefaultLanguage must be 'en' or 'nl'." });
        }

        var options = new TracksideLocalizationOptions { DefaultLanguage = request.DefaultLanguage };
        await configurationStore.SaveLocalizationAsync(options, cancellationToken);
        if (configuration is IConfigurationRoot configurationRoot)
        {
            configurationRoot.Reload();
        }

        return Results.Ok(LocalizationResponse.From(options));
    }

    private static async Task<IResult> GetBestLapsAsync(
        string? window,
        string? mode,
        string? trackName,
        string? vehicleName,
        string? sessionKind,
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

        var parsedSessionKind = ParseSessionKindFilter(sessionKind);
        if (parsedSessionKind is null && !string.IsNullOrWhiteSpace(sessionKind))
        {
            return Results.BadRequest(new { error = "Session kind must be practice, qualifying, race, or unknown." });
        }

        var nowUtc = timeProvider.GetUtcNow();
        var monthlyTrack = await store.GetActiveMonthlyTrackAsync(cancellationToken);
        var bounds = BuildBestLapWindow(normalizedWindow, trackName, monthlyTrack, nowUtc);
        var rows = await store.GetBestLapsAsync(new HistoricalBestLapQuery
        {
            FromUtc = bounds.FromUtc,
            ToUtc = bounds.ToUtc,
            TrackName = bounds.TrackName,
            VehicleName = string.IsNullOrWhiteSpace(vehicleName) ? null : vehicleName.Trim(),
            SessionKind = parsedSessionKind,
            Mode = boardMode.Value,
            Limit = limit ?? 20,
            SortByTrack = string.IsNullOrWhiteSpace(bounds.TrackName),
        }, cancellationToken);

        return Results.Ok(new BestLapBoardResponse
        {
            Window = normalizedWindow,
            Mode = ToBestLapBoardModeValue(boardMode.Value),
            TrackName = bounds.TrackName,
            VehicleName = string.IsNullOrWhiteSpace(vehicleName) ? null : vehicleName.Trim(),
            SessionKind = parsedSessionKind,
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

    private static async Task<IResult> GetHistoricalSessionsAsync(
        int? limit,
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        var sessions = await store.GetHistoricalSessionsAsync(new HistoricalSessionQuery
        {
            Limit = limit ?? 50,
        }, cancellationToken);
        return Results.Ok(sessions.Select(HistoricalSessionSummaryResponse.From).ToList());
    }

    private static async Task<IResult> DeleteEmptyHistoricalSessionsAsync(
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        var deleted = await store.DeleteEmptyHistoricalSessionsAsync(cancellationToken);
        return Results.Ok(new DeleteSessionsResponse(deleted));
    }

    private static async Task<IResult> GetHistoricalSessionAsync(
        string sessionId,
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        var session = await store.GetHistoricalSessionAsync(sessionId, cancellationToken);
        return session is null
            ? Results.NotFound(new { error = "Session not found." })
            : Results.Ok(HistoricalSessionDetailResponse.From(session));
    }

    private static async Task<IResult> DeleteHistoricalSessionAsync(
        string sessionId,
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        var deleted = await store.DeleteHistoricalSessionAsync(sessionId, cancellationToken);
        return deleted
            ? Results.NoContent()
            : Results.NotFound(new { error = "Session not found." });
    }

    private static async Task<IResult> SetSessionHistoryInclusionAsync(
        string sessionId,
        SessionHistoryRequest request,
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        var updated = await store.SetSessionCountForHistoryAsync(sessionId, request.CountForHistory, cancellationToken);
        if (!updated)
        {
            return Results.NotFound(new { error = "Session not found." });
        }

        return await GetHistoricalSessionAsync(sessionId, store, cancellationToken);
    }

    private static async Task<IResult> CorrectParticipantAsync(
        string sessionId,
        long participantId,
        ParticipantCorrectionRequest request,
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        var session = await store.CorrectParticipantAsync(sessionId, participantId, request, cancellationToken);
        return session is null
            ? Results.NotFound(new { error = "Participant not found." })
            : Results.Ok(HistoricalSessionDetailResponse.From(session));
    }

    private static async Task<IResult> CorrectLapAsync(
        string sessionId,
        long lapId,
        LapCorrectionRequest request,
        ITracksideStore store,
        CancellationToken cancellationToken)
    {
        try
        {
            var session = await store.CorrectLapAsync(sessionId, lapId, request, cancellationToken);
            return session is null
                ? Results.NotFound(new { error = "Lap not found." })
                : Results.Ok(HistoricalSessionDetailResponse.From(session));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }

    private static IResult GetKioskSettings(IOptionsMonitor<TracksideOptions> options) => Results.Ok(KioskSettingsResponse.From(options.CurrentValue.Kiosk));

    private static IResult GetDriverTrackerSettings(IOptionsMonitor<TracksideOptions> options) => Results.Ok(DriverTrackerSettingsResponse.From(options.CurrentValue.DriverTracker));

    private static IResult GetDriverTrackerTracks(TrackGeometryRecorder trackGeometryRecorder) => Results.Ok(new DriverTrackerTracksResponse
    {
        Tracks = trackGeometryRecorder.ListTracks(),
    });

    private static async Task<IResult> SaveKioskSettingsAsync(
        KioskSettingsRequest request,
        TracksideWritableConfigurationStore configurationStore,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var options = new TracksideKioskOptions { DefaultDisplayMode = request.DefaultDisplayMode };
        await configurationStore.SaveKioskAsync(options, cancellationToken);
        if (configuration is IConfigurationRoot configurationRoot)
        {
            configurationRoot.Reload();
        }

        return Results.Ok(KioskSettingsResponse.From(options));
    }

    private static async Task<IResult> SaveDriverTrackerSettingsAsync(
        DriverTrackerSettingsRequest request,
        TracksideWritableConfigurationStore configurationStore,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        if (!double.IsFinite(request.ClientRefreshHz) || request.ClientRefreshHz is < TracksideDriverTrackerOptions.MinimumClientRefreshHz or > TracksideDriverTrackerOptions.MaximumClientRefreshHz)
        {
            return Results.BadRequest(new { error = "ClientRefreshHz must be between 1 and 60." });
        }

        if (request.GeometryRecordingLaps is < TracksideDriverTrackerOptions.MinimumGeometryRecordingLaps or > TracksideDriverTrackerOptions.MaximumGeometryRecordingLaps)
        {
            return Results.BadRequest(new { error = "GeometryRecordingLaps must be between 1 and 20." });
        }

        var options = new TracksideDriverTrackerOptions
        {
            ClientRefreshHz = Math.Round(request.ClientRefreshHz, 2),
            GeometryRecordingLaps = request.GeometryRecordingLaps,
        };
        await configurationStore.SaveDriverTrackerAsync(options, cancellationToken);
        if (configuration is IConfigurationRoot configurationRoot)
        {
            configurationRoot.Reload();
        }

        return Results.Ok(DriverTrackerSettingsResponse.From(options));
    }

    private static async Task<IResult> StartDriverTrackerRecordingAsync(
        DriverTrackerRecordingRequest request,
        TrackGeometryRecorder trackGeometryRecorder,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TrackName))
        {
            return Results.BadRequest(new { error = "TrackName is required." });
        }

        if (request.TargetCompletedLaps is < TracksideDriverTrackerOptions.MinimumGeometryRecordingLaps or > TracksideDriverTrackerOptions.MaximumGeometryRecordingLaps)
        {
            return Results.BadRequest(new { error = "TargetCompletedLaps must be between 1 and 20." });
        }

        var result = await trackGeometryRecorder.StartRecordingAsync(new TrackGeometryRecordingRequest
        {
            TrackName = request.TrackName,
            TargetCompletedLaps = request.TargetCompletedLaps,
            ResetExistingGeometry = request.ResetExistingGeometry,
        }, cancellationToken);

        return Results.Ok(result);
    }

    private static async Task<IResult> EnforceRetentionAsync(
        ITracksideStore store,
        IOptionsMonitor<TracksideOptions> options,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var result = await store.EnforceRetentionAsync(options.CurrentValue.Persistence.Retention, timeProvider.GetUtcNow(), cancellationToken);
        return Results.Ok(RetentionCleanupResponse.From(result));
    }

    private static IResult GetSharedMemoryDebug(
        IOptionsMonitor<TracksideOptions> options,
        IRf2SharedMemoryMapDiscovery mapDiscovery,
        Rf2ScoringMapResolver mapResolver,
        Rf2SharedMemoryMapReader mapReader,
        IRf2ScoringPayloadParser scoringParser,
        IRf2TelemetryPayloadParser telemetryParser,
        MappedBufferPayloadLocator payloadLocator,
        TimeProvider timeProvider)
    {
        var source = options.CurrentValue.Source;
        var sharedMemory = source.SharedMemory;
        var discovered = sharedMemory.AutoDiscover ? mapDiscovery.DiscoverScoringMaps(sharedMemory) : [];
        var resolution = mapResolver.Resolve(sharedMemory, discovered);
        var scoring = BuildScoringDebug(resolution, mapReader, scoringParser, payloadLocator);
        var telemetryCandidateSource = scoring.MapName ?? resolution.CandidateMapNames.FirstOrDefault();
        var telemetryCandidates = Rf2SharedMemoryMapReader.CandidateMapNames(
            Rf2SharedMemoryMapReader.TelemetryMapName,
            DeriveTelemetryMapName(telemetryCandidateSource),
            sharedMemory.ProcessId);

        return Results.Ok(new SharedMemoryDebugResponse
        {
            TimestampUtc = timeProvider.GetUtcNow(),
            SourceMode = source.Mode,
            TelemetryEnabled = sharedMemory.Telemetry.Enabled,
            DiscoveryStatus = resolution.Status,
            DiscoveredScoringMaps = discovered,
            Scoring = scoring,
            Telemetry = BuildTelemetryDebug(sharedMemory.Telemetry.Enabled, telemetryCandidates, mapReader, telemetryParser, payloadLocator),
        });
    }

    private static SharedMemoryMapDebugResponse BuildScoringDebug(
        Rf2ScoringMapResolution resolution,
        Rf2SharedMemoryMapReader mapReader,
        IRf2ScoringPayloadParser parser,
        MappedBufferPayloadLocator payloadLocator)
    {
        if (resolution.IsAmbiguous)
        {
            return SharedMemoryMapDebugResponse.Unavailable("scoring", resolution.Status, [], null);
        }

        if (!mapReader.TryReadFirstAvailable(resolution.CandidateMapNames, parser.PayloadSize, out var read, out var status))
        {
            return SharedMemoryMapDebugResponse.Unavailable("scoring", status, resolution.CandidateMapNames, null);
        }

        try
        {
            var location = payloadLocator.Locate(read.Buffer, parser.PayloadSize, parser.ScorePayload);
            var payload = read.Buffer.AsSpan(location.Offset, location.PayloadSize);
            var stable = parser.IsStablePayload(payload);
            var source = stable ? parser.ParseSource(payload, "shared-memory", status) : null;
            return new SharedMemoryMapDebugResponse
            {
                Kind = "scoring",
                IsAvailable = true,
                Status = status,
                CandidateMapNames = resolution.CandidateMapNames,
                MapName = read.MapName,
                BytesRead = read.Buffer.Length,
                DecodeOffset = location.Offset,
                PayloadSize = location.PayloadSize,
                Score = location.Score,
                IsStable = stable,
                TrackName = source?.Session.TrackName,
                VehicleCount = source?.Drivers.Count,
                Vehicles = source?.Drivers.Take(12).Select(SharedMemoryDebugVehicle.FromScoring).ToList() ?? [],
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException)
        {
            return SharedMemoryMapDebugResponse.Unavailable("scoring", status, resolution.CandidateMapNames, ex.Message) with { MapName = read.MapName, BytesRead = read.Buffer.Length };
        }
    }

    private static SharedMemoryMapDebugResponse BuildTelemetryDebug(
        bool telemetryEnabled,
        IReadOnlyList<string> candidateMapNames,
        Rf2SharedMemoryMapReader mapReader,
        IRf2TelemetryPayloadParser parser,
        MappedBufferPayloadLocator payloadLocator)
    {
        if (!telemetryEnabled)
        {
            return SharedMemoryMapDebugResponse.Unavailable("telemetry", "telemetry loop disabled", candidateMapNames, null);
        }

        if (!mapReader.TryReadFirstAvailable(candidateMapNames, parser.PayloadSize, out var read, out var status))
        {
            return SharedMemoryMapDebugResponse.Unavailable("telemetry", status, candidateMapNames, null);
        }

        try
        {
            var location = payloadLocator.Locate(read.Buffer, parser.PayloadSize, parser.ScorePayload);
            var payload = read.Buffer.AsSpan(location.Offset, location.PayloadSize);
            var stable = parser.IsStablePayload(payload);
            var frame = stable ? parser.ParsePositionFrame(payload, "telemetry") : null;
            return new SharedMemoryMapDebugResponse
            {
                Kind = "telemetry",
                IsAvailable = true,
                Status = status,
                CandidateMapNames = candidateMapNames,
                MapName = read.MapName,
                BytesRead = read.Buffer.Length,
                DecodeOffset = location.Offset,
                PayloadSize = location.PayloadSize,
                Score = location.Score,
                IsStable = stable,
                TrackName = frame?.TrackName,
                VehicleCount = frame?.Vehicles.Count,
                Vehicles = frame?.Vehicles.Take(12).Select(SharedMemoryDebugVehicle.FromTelemetry).ToList() ?? [],
            };
        }
        catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException)
        {
            return SharedMemoryMapDebugResponse.Unavailable("telemetry", status, candidateMapNames, ex.Message) with { MapName = read.MapName, BytesRead = read.Buffer.Length };
        }
    }

    private static string? DeriveTelemetryMapName(string? scoringMapName)
    {
        if (string.IsNullOrWhiteSpace(scoringMapName))
        {
            return null;
        }

        var telemetryMapName = scoringMapName.Replace("Scoring", "Telemetry", StringComparison.OrdinalIgnoreCase);
        return string.Equals(telemetryMapName, scoringMapName, StringComparison.OrdinalIgnoreCase)
            ? null
            : telemetryMapName;
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

    private static SessionKind? ParseSessionKindFilter(string? sessionKind)
    {
        return string.IsNullOrWhiteSpace(sessionKind)
            ? null
            : Enum.TryParse<SessionKind>(sessionKind.Trim(), ignoreCase: true, out var parsed)
                ? parsed
                : null;
    }

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
/// Request to include or exclude a persisted session from historical boards.
/// </summary>
public sealed record SessionHistoryRequest
{
    /// <summary>
    /// True when the session should count for historical boards.
    /// </summary>
    public bool CountForHistory { get; init; }
}

/// <summary>
/// Response for bulk stored-session deletion operations.
/// </summary>
/// <param name="DeletedCount">Number of stored sessions deleted.</param>
public sealed record DeleteSessionsResponse(int DeletedCount);

/// <summary>
/// Kiosk display settings response.
/// </summary>
public sealed record KioskSettingsResponse
{
    /// <summary>
    /// Default display mode for newly opened kiosk screens.
    /// </summary>
    public KioskDisplayMode DefaultDisplayMode { get; init; }

    /// <summary>
    /// Maps configured kiosk options to an API response.
    /// </summary>
    /// <param name="options">Kiosk options.</param>
    /// <returns>API response.</returns>
    public static KioskSettingsResponse From(TracksideKioskOptions options) => new()
    {
        DefaultDisplayMode = options.DefaultDisplayMode,
    };
}

/// <summary>
/// Kiosk display settings save request.
/// </summary>
public sealed record KioskSettingsRequest
{
    /// <summary>
    /// Default display mode for newly opened kiosk screens.
    /// </summary>
    public KioskDisplayMode DefaultDisplayMode { get; init; } = KioskDisplayMode.Monthly;
}

/// <summary>
/// Driver tracker settings response.
/// </summary>
public sealed record DriverTrackerSettingsResponse
{
    /// <summary>
    /// Browser-side tracker refresh/redraw rate in Hertz. Source freshness is determined separately.
    /// </summary>
    public double ClientRefreshHz { get; init; } = TracksideDriverTrackerOptions.DefaultClientRefreshHz;

    /// <summary>
    /// Default complete lap passes recorded before generated geometry is considered complete.
    /// </summary>
    public int GeometryRecordingLaps { get; init; } = TracksideDriverTrackerOptions.DefaultGeometryRecordingLaps;

    /// <summary>
    /// Maps configured driver tracker options to an API response.
    /// </summary>
    public static DriverTrackerSettingsResponse From(TracksideDriverTrackerOptions options) => new()
    {
        ClientRefreshHz = options.ClientRefreshHz,
        GeometryRecordingLaps = options.GeometryRecordingLaps,
    };
}

/// <summary>
/// Driver tracker settings save request.
/// </summary>
public sealed record DriverTrackerSettingsRequest
{
    /// <summary>
    /// Browser-side tracker refresh/redraw rate in Hertz. Source freshness is determined separately.
    /// </summary>
    public double ClientRefreshHz { get; init; } = TracksideDriverTrackerOptions.DefaultClientRefreshHz;

    /// <summary>
    /// Default complete lap passes recorded before generated geometry is considered complete.
    /// </summary>
    public int GeometryRecordingLaps { get; init; } = TracksideDriverTrackerOptions.DefaultGeometryRecordingLaps;
}

/// <summary>
/// Driver tracker generated-geometry track catalog response.
/// </summary>
public sealed record DriverTrackerTracksResponse
{
    /// <summary>
    /// Seen and persisted tracks.
    /// </summary>
    public IReadOnlyList<TrackGeometryCatalogEntry> Tracks { get; init; } = [];
}

/// <summary>
/// Request to start or improve generated geometry for a track.
/// </summary>
public sealed record DriverTrackerRecordingRequest
{
    /// <summary>
    /// Track to record.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Complete lap passes to average into this recording pass.
    /// </summary>
    public int TargetCompletedLaps { get; init; } = TracksideDriverTrackerOptions.DefaultGeometryRecordingLaps;

    /// <summary>
    /// True to replace existing generated geometry before recording.
    /// </summary>
    public bool ResetExistingGeometry { get; init; } = true;
}

/// <summary>
/// Realtime shared-memory diagnostic response for admin troubleshooting.
/// </summary>
public sealed record SharedMemoryDebugResponse
{
    /// <summary>
    /// Time the diagnostic snapshot was produced.
    /// </summary>
    public DateTimeOffset TimestampUtc { get; init; }

    /// <summary>
    /// Configured live source mode.
    /// </summary>
    public LiveSessionSourceMode SourceMode { get; init; }

    /// <summary>
    /// True when the telemetry loop is enabled in configuration.
    /// </summary>
    public bool TelemetryEnabled { get; init; }

    /// <summary>
    /// Scoring map discovery status.
    /// </summary>
    public string DiscoveryStatus { get; init; } = string.Empty;

    /// <summary>
    /// Discovered scoring map candidates.
    /// </summary>
    public IReadOnlyList<Rf2ScoringMapCandidate> DiscoveredScoringMaps { get; init; } = [];

    /// <summary>
    /// Current scoring map read result.
    /// </summary>
    public SharedMemoryMapDebugResponse Scoring { get; init; } = SharedMemoryMapDebugResponse.Unavailable("scoring", "not read", [], null);

    /// <summary>
    /// Current telemetry map read result.
    /// </summary>
    public SharedMemoryMapDebugResponse Telemetry { get; init; } = SharedMemoryMapDebugResponse.Unavailable("telemetry", "not read", [], null);
}

/// <summary>
/// One shared-memory map diagnostic read result.
/// </summary>
public sealed record SharedMemoryMapDebugResponse
{
    /// <summary>
    /// Map kind: scoring or telemetry.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// True when a map was opened and read.
    /// </summary>
    public bool IsAvailable { get; init; }

    /// <summary>
    /// Read or discovery status.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Candidate map names tried in order.
    /// </summary>
    public IReadOnlyList<string> CandidateMapNames { get; init; } = [];

    /// <summary>
    /// Map name that was read.
    /// </summary>
    public string? MapName { get; init; }

    /// <summary>
    /// Number of bytes copied from the map.
    /// </summary>
    public int? BytesRead { get; init; }

    /// <summary>
    /// Payload decode offset inside the mapped buffer.
    /// </summary>
    public int? DecodeOffset { get; init; }

    /// <summary>
    /// Expected parser payload size.
    /// </summary>
    public int? PayloadSize { get; init; }

    /// <summary>
    /// Payload plausibility score.
    /// </summary>
    public int? Score { get; init; }

    /// <summary>
    /// True when begin/end update counters match.
    /// </summary>
    public bool? IsStable { get; init; }

    /// <summary>
    /// Parsed track name, when available.
    /// </summary>
    public string? TrackName { get; init; }

    /// <summary>
    /// Parsed vehicle count, when available.
    /// </summary>
    public int? VehicleCount { get; init; }

    /// <summary>
    /// Small preview of parsed vehicle values.
    /// </summary>
    public IReadOnlyList<SharedMemoryDebugVehicle> Vehicles { get; init; } = [];

    /// <summary>
    /// Parser or read error, when available.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates an unavailable read result.
    /// </summary>
    public static SharedMemoryMapDebugResponse Unavailable(string kind, string status, IReadOnlyList<string> candidateMapNames, string? error) => new()
    {
        Kind = kind,
        Status = status,
        CandidateMapNames = candidateMapNames,
        Error = error,
    };
}

/// <summary>
/// Small vehicle preview for shared-memory diagnostics.
/// </summary>
public sealed record SharedMemoryDebugVehicle
{
    /// <summary>
    /// Vehicle/scoring id.
    /// </summary>
    public string DriverId { get; init; } = string.Empty;

    /// <summary>
    /// Display or rig name when available.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Lap progress percent from scoring.
    /// </summary>
    public double? TrackPositionPercent { get; init; }

    /// <summary>
    /// X coordinate.
    /// </summary>
    public double? PosX { get; init; }

    /// <summary>
    /// Y coordinate.
    /// </summary>
    public double? PosY { get; init; }

    /// <summary>
    /// Z coordinate.
    /// </summary>
    public double? PosZ { get; init; }

    /// <summary>
    /// True when scoring reports the vehicle in pits.
    /// </summary>
    public bool? IsInPits { get; init; }

    /// <summary>
    /// True when scoring reports the vehicle in a garage stall.
    /// </summary>
    public bool? IsInGarageStall { get; init; }

    /// <summary>
    /// Valid lap flag from scoring.
    /// </summary>
    public int? ValidLapFlag { get; init; }

    /// <summary>
    /// Maps a scoring driver to a debug vehicle preview.
    /// </summary>
    public static SharedMemoryDebugVehicle FromScoring(LeaderboardDriverSource driver) => new()
    {
        DriverId = driver.DriverId,
        Name = driver.RigName,
        TrackPositionPercent = driver.TrackPositionPercent,
        PosX = driver.PosX,
        PosY = driver.PosY,
        PosZ = driver.PosZ,
        IsInPits = driver.IsInPits,
        IsInGarageStall = driver.IsInGarageStall,
        ValidLapFlag = driver.ValidLapFlag,
    };

    /// <summary>
    /// Maps a telemetry position vehicle to a debug vehicle preview.
    /// </summary>
    public static SharedMemoryDebugVehicle FromTelemetry(TelemetryPositionVehicle vehicle) => new()
    {
        DriverId = vehicle.DriverId,
        PosX = vehicle.PosX,
        PosY = vehicle.PosY,
        PosZ = vehicle.PosZ,
    };
}

/// <summary>
/// Frontend localization settings response.
/// </summary>
public sealed record LocalizationResponse
{
    /// <summary>
    /// Default language for frontend UI strings.
    /// </summary>
    public string DefaultLanguage { get; init; } = "en";

    /// <summary>
    /// Maps localization options to an API response.
    /// </summary>
    public static LocalizationResponse From(TracksideLocalizationOptions options) => new()
    {
        DefaultLanguage = options.DefaultLanguage,
    };
}

/// <summary>
/// Frontend localization settings save request.
/// </summary>
public sealed record LocalizationRequest
{
    /// <summary>
    /// Default language for frontend UI strings.
    /// </summary>
    public string DefaultLanguage { get; init; } = "en";
}

/// <summary>
/// Persistence retention cleanup response.
/// </summary>
public sealed record RetentionCleanupResponse
{
    /// <summary>
    /// Raw lap rows deleted.
    /// </summary>
    public int DetailedLapRecordsDeleted { get; init; }

    /// <summary>
    /// Session rows deleted.
    /// </summary>
    public int SessionSummariesDeleted { get; init; }

    /// <summary>
    /// Derived track-best rows deleted.
    /// </summary>
    public int TrackBestRecordsDeleted { get; init; }

    /// <summary>
    /// Monthly track period rows deleted.
    /// </summary>
    public int MonthlyTrackPeriodsDeleted { get; init; }

    /// <summary>
    /// Maps a store cleanup result to an API response.
    /// </summary>
    /// <param name="result">Store cleanup result.</param>
    /// <returns>API response.</returns>
    public static RetentionCleanupResponse From(TracksideRetentionCleanupResult result) => new()
    {
        DetailedLapRecordsDeleted = result.DetailedLapRecordsDeleted,
        SessionSummariesDeleted = result.SessionSummariesDeleted,
        TrackBestRecordsDeleted = result.TrackBestRecordsDeleted,
        MonthlyTrackPeriodsDeleted = result.MonthlyTrackPeriodsDeleted,
    };
}

/// <summary>
/// Persisted session summary for the admin session browser.
/// </summary>
public record HistoricalSessionSummaryResponse
{
    /// <summary>
    /// Durable session identifier.
    /// </summary>
    public string SessionId { get; init; } = string.Empty;

    /// <summary>
    /// Source that produced the session.
    /// </summary>
    public string Source { get; init; } = string.Empty;

    /// <summary>
    /// Track name associated with the session.
    /// </summary>
    public string TrackName { get; init; } = string.Empty;

    /// <summary>
    /// Session kind associated with the session.
    /// </summary>
    public SessionKind SessionKind { get; init; }

    /// <summary>
    /// Latest observed session phase.
    /// </summary>
    public SessionPhase SessionPhase { get; init; }

    /// <summary>
    /// First time Trackside observed this session.
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; init; }

    /// <summary>
    /// Most recent time Trackside observed this session.
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; }

    /// <summary>
    /// Latest observed vehicle count.
    /// </summary>
    public int VehicleCount { get; init; }

    /// <summary>
    /// True when this session contributes to historical boards.
    /// </summary>
    public bool CountForHistory { get; init; }

    /// <summary>
    /// Number of persisted participants in this session.
    /// </summary>
    public int ParticipantCount { get; init; }

    /// <summary>
    /// Number of persisted completed laps in this session.
    /// </summary>
    public int LapCount { get; init; }

    /// <summary>
    /// Number of persisted laps that count for timing boards.
    /// </summary>
    public int ValidTimedLapCount { get; init; }

    /// <summary>
    /// Fastest valid timed lap in the session, when any.
    /// </summary>
    public double? BestLapSeconds { get; init; }

    /// <summary>
    /// Maps a store session summary to an API response.
    /// </summary>
    /// <param name="session">Store session summary.</param>
    /// <returns>API response.</returns>
    public static HistoricalSessionSummaryResponse From(HistoricalSessionSummary session) => new()
    {
        SessionId = session.SessionId,
        Source = session.Source,
        TrackName = session.TrackName,
        SessionKind = session.SessionKind,
        SessionPhase = session.SessionPhase,
        FirstSeenUtc = session.FirstSeenUtc,
        LastSeenUtc = session.LastSeenUtc,
        VehicleCount = session.VehicleCount,
        CountForHistory = session.CountForHistory,
        ParticipantCount = session.ParticipantCount,
        LapCount = session.LapCount,
        ValidTimedLapCount = session.ValidTimedLapCount,
        BestLapSeconds = session.BestLapSeconds,
    };
}

/// <summary>
/// Persisted session detail for the admin session browser.
/// </summary>
public sealed record HistoricalSessionDetailResponse : HistoricalSessionSummaryResponse
{
    /// <summary>
    /// Participants observed in this session.
    /// </summary>
    public IReadOnlyList<HistoricalSessionParticipantResponse> Participants { get; init; } = [];

    /// <summary>
    /// Maps a store session detail to an API response.
    /// </summary>
    /// <param name="session">Store session detail.</param>
    /// <returns>API response.</returns>
    public static HistoricalSessionDetailResponse From(HistoricalSessionDetail session) => new()
    {
        SessionId = session.SessionId,
        Source = session.Source,
        TrackName = session.TrackName,
        SessionKind = session.SessionKind,
        SessionPhase = session.SessionPhase,
        FirstSeenUtc = session.FirstSeenUtc,
        LastSeenUtc = session.LastSeenUtc,
        VehicleCount = session.VehicleCount,
        CountForHistory = session.CountForHistory,
        ParticipantCount = session.ParticipantCount,
        LapCount = session.LapCount,
        ValidTimedLapCount = session.ValidTimedLapCount,
        BestLapSeconds = session.BestLapSeconds,
        Participants = session.Participants.Select(HistoricalSessionParticipantResponse.From).ToList(),
    };
}

/// <summary>
/// Persisted participant row for a session detail response.
/// </summary>
public sealed record HistoricalSessionParticipantResponse
{
    /// <summary>
    /// Durable participant row identifier.
    /// </summary>
    public long ParticipantId { get; init; }

    /// <summary>
    /// Latest display rank for the participant.
    /// </summary>
    public int Rank { get; init; }

    /// <summary>
    /// Fixed rig or rFactor 2 name.
    /// </summary>
    public string RigName { get; init; } = string.Empty;

    /// <summary>
    /// Screen name captured for this participant.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Corrected display name entered by staff, when any.
    /// </summary>
    public string? DisplayNameOverride { get; init; }

    /// <summary>
    /// Effective display name after staff correction.
    /// </summary>
    public string EffectiveDisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Optional linked driver profile id.
    /// </summary>
    public string? DriverProfileId { get; init; }

    /// <summary>
    /// Vehicle name captured for this participant.
    /// </summary>
    public string VehicleName { get; init; } = string.Empty;

    /// <summary>
    /// First time Trackside observed this participant in the session.
    /// </summary>
    public DateTimeOffset FirstSeenUtc { get; init; }

    /// <summary>
    /// Most recent time Trackside observed this participant in the session.
    /// </summary>
    public DateTimeOffset LastSeenUtc { get; init; }

    /// <summary>
    /// Latest completed lap count.
    /// </summary>
    public int CompletedLaps { get; init; }

    /// <summary>
    /// Best valid timed lap in seconds when available.
    /// </summary>
    public double? BestLapSeconds { get; init; }

    /// <summary>
    /// Most recently completed lap time in seconds when available.
    /// </summary>
    public double? LastLapSeconds { get; init; }

    /// <summary>
    /// Number of persisted completed laps for this participant.
    /// </summary>
    public int LapCount { get; init; }

    /// <summary>
    /// Number of persisted completed laps that count for timing boards.
    /// </summary>
    public int ValidTimedLapCount { get; init; }

    /// <summary>
    /// True when staff excluded this participant from public results and boards.
    /// </summary>
    public bool ExcludedFromHistory { get; init; }

    /// <summary>
    /// Optional staff correction reason.
    /// </summary>
    public string? CorrectionReason { get; init; }

    /// <summary>
    /// Persisted lap rows for this participant.
    /// </summary>
    public IReadOnlyList<HistoricalSessionLapResponse> Laps { get; init; } = [];

    /// <summary>
    /// Maps a store participant row to an API response.
    /// </summary>
    /// <param name="participant">Store participant row.</param>
    /// <returns>API response.</returns>
    public static HistoricalSessionParticipantResponse From(HistoricalSessionParticipant participant) => new()
    {
        ParticipantId = participant.ParticipantId,
        Rank = participant.Rank,
        RigName = participant.RigName,
        DisplayName = participant.DisplayName,
        DisplayNameOverride = participant.DisplayNameOverride,
        EffectiveDisplayName = participant.EffectiveDisplayName,
        DriverProfileId = participant.DriverProfileId,
        VehicleName = participant.VehicleName,
        FirstSeenUtc = participant.FirstSeenUtc,
        LastSeenUtc = participant.LastSeenUtc,
        CompletedLaps = participant.CompletedLaps,
        BestLapSeconds = participant.BestLapSeconds,
        LastLapSeconds = participant.LastLapSeconds,
        LapCount = participant.LapCount,
        ValidTimedLapCount = participant.ValidTimedLapCount,
        ExcludedFromHistory = participant.ExcludedFromHistory,
        CorrectionReason = participant.CorrectionReason,
        Laps = participant.Laps.Select(HistoricalSessionLapResponse.From).ToList(),
    };
}

/// <summary>
/// Persisted lap row for a session detail response.
/// </summary>
public sealed record HistoricalSessionLapResponse
{
    /// <summary>
    /// Durable lap row identifier.
    /// </summary>
    public long LapId { get; init; }

    /// <summary>
    /// Durable participant row identifier.
    /// </summary>
    public long ParticipantId { get; init; }

    /// <summary>
    /// Completed lap number within the session.
    /// </summary>
    public int LapNumber { get; init; }

    /// <summary>
    /// Captured lap time in seconds.
    /// </summary>
    public double LapSeconds { get; init; }

    /// <summary>
    /// Staff-entered corrected lap time in seconds.
    /// </summary>
    public double? LapSecondsOverride { get; init; }

    /// <summary>
    /// Effective lap time after staff correction.
    /// </summary>
    public double EffectiveLapSeconds { get; init; }

    /// <summary>
    /// rFactor 2 valid-lap flag captured for this lap.
    /// </summary>
    public int? ValidLapFlag { get; init; }

    /// <summary>
    /// True when this lap currently counts for timing boards.
    /// </summary>
    public bool CountsForTiming { get; init; }

    /// <summary>
    /// True when staff invalidated this lap.
    /// </summary>
    public bool StaffInvalidated { get; init; }

    /// <summary>
    /// Optional staff correction reason.
    /// </summary>
    public string? CorrectionReason { get; init; }

    /// <summary>
    /// UTC timestamp when Trackside observed this lap.
    /// </summary>
    public DateTimeOffset ObservedUtc { get; init; }

    /// <summary>
    /// Maps a store lap row to an API response.
    /// </summary>
    /// <param name="lap">Store lap row.</param>
    /// <returns>API response.</returns>
    public static HistoricalSessionLapResponse From(HistoricalSessionLap lap) => new()
    {
        LapId = lap.LapId,
        ParticipantId = lap.ParticipantId,
        LapNumber = lap.LapNumber,
        LapSeconds = lap.LapSeconds,
        LapSecondsOverride = lap.LapSecondsOverride,
        EffectiveLapSeconds = lap.EffectiveLapSeconds,
        ValidLapFlag = lap.ValidLapFlag,
        CountsForTiming = lap.CountsForTiming,
        StaffInvalidated = lap.StaffInvalidated,
        CorrectionReason = lap.CorrectionReason,
        ObservedUtc = lap.ObservedUtc,
    };
}

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
    /// Vehicle/content filter used by the board, when any.
    /// </summary>
    public string? VehicleName { get; init; }

    /// <summary>
    /// Session-kind filter used by the board, when any.
    /// </summary>
    public SessionKind? SessionKind { get; init; }

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
