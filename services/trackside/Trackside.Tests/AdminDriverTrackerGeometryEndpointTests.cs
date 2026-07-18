using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;
using Trackside.Application.LiveSession;
using Trackside.Service.Api;
using Trackside.Service.Configuration;
using Trackside.Service.Hosting;
using Trackside.Service.Tracking;

namespace Trackside.Tests;

/// <summary>
/// Verifies selected-track geometry endpoint responses and authorization metadata.
/// </summary>
public sealed class AdminDriverTrackerGeometryEndpointTests
{
    /// <summary>
    /// Missing track names are rejected before catalog lookup.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsMissingTrackName(string? trackName)
    {
        var recorder = CreateRecorder(out var tempRoot);
        try
        {
            var result = TracksideApiEndpoints.GetDriverTrackerTrackGeometry(trackName, recorder);

            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Names absent from the recorder catalog do not create phantom track entries.
    /// </summary>
    [Fact]
    public void ReturnsNotFoundForUnknownTrack()
    {
        var recorder = CreateRecorder(out var tempRoot);
        try
        {
            var result = TracksideApiEndpoints.GetDriverTrackerTrackGeometry("Unknown Track", recorder);

            Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            Assert.Empty(recorder.ListTracks());
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Catalog tracks return their current geometry state even before an outline is complete.
    /// </summary>
    [Fact]
    public async Task ReturnsGeometryForKnownCatalogTrack()
    {
        var recorder = CreateRecorder(out var tempRoot);
        try
        {
            await recorder.StartRecordingAsync(new TrackGeometryRecordingRequest
            {
                TrackName = "Loch Drummond - Short",
                TargetCompletedLaps = 1,
                ResetExistingGeometry = true,
            }, CancellationToken.None);

            var result = TracksideApiEndpoints.GetDriverTrackerTrackGeometry("loch drummond - short", recorder);

            Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            var geometry = Assert.IsType<TrackGeometryResponse>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
            Assert.Equal("Loch Drummond - Short", geometry.TrackName);
            Assert.False(geometry.IsAvailable);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Missing track names are rejected before delete lookup.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task DeleteRejectsMissingTrackName(string? trackName)
    {
        var recorder = CreateRecorder(out var tempRoot);
        try
        {
            var result = await TracksideApiEndpoints.DeleteDriverTrackerTrackGeometryAsync(trackName, recorder, CancellationToken.None);

            Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Delete returns not found for names outside the known catalog.
    /// </summary>
    [Fact]
    public async Task DeleteReturnsNotFoundForUnknownTrack()
    {
        var recorder = CreateRecorder(out var tempRoot);
        try
        {
            var result = await TracksideApiEndpoints.DeleteDriverTrackerTrackGeometryAsync("Unknown Track", recorder, CancellationToken.None);

            Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            Assert.Empty(recorder.ListTracks());
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Deleting a known track returns its updated catalog state with geometry cleared.
    /// </summary>
    [Fact]
    public async Task DeleteReturnsUpdatedCatalogEntryForKnownTrack()
    {
        var recorder = CreateRecorder(out var tempRoot);
        try
        {
            await recorder.StartRecordingAsync(new TrackGeometryRecordingRequest
            {
                TrackName = "Loch Drummond - Short",
                TargetCompletedLaps = 1,
                ResetExistingGeometry = true,
            }, CancellationToken.None);

            var result = await TracksideApiEndpoints.DeleteDriverTrackerTrackGeometryAsync("Loch Drummond - Short", recorder, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            var entry = Assert.IsType<TrackGeometryCatalogEntry>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
            Assert.Equal("Loch Drummond - Short", entry.TrackName);
            Assert.False(entry.HasGeometry);
            Assert.Equal(0, entry.SampleCount);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Delete lookup is case-insensitive and returns canonical catalog track casing.
    /// </summary>
    [Fact]
    public async Task DeleteResolvesCatalogTrackCaseInsensitively()
    {
        var recorder = CreateRecorder(out var tempRoot);
        try
        {
            await recorder.StartRecordingAsync(new TrackGeometryRecordingRequest
            {
                TrackName = "Loch Drummond - Short",
                TargetCompletedLaps = 1,
                ResetExistingGeometry = true,
            }, CancellationToken.None);

            var result = await TracksideApiEndpoints.DeleteDriverTrackerTrackGeometryAsync("loch drummond - short", recorder, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            var entry = Assert.IsType<TrackGeometryCatalogEntry>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
            Assert.Equal("Loch Drummond - Short", entry.TrackName);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// The selected-track geometry GET and DELETE routes remain restricted to authenticated administrators.
    /// </summary>
    [Fact]
    public async Task RouteRequiresAuthorization()
    {
        var recorder = CreateRecorder(out var tempRoot);
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(recorder);
        await using var app = builder.Build();
        try
        {
            app.MapAdminDriverTrackerGeometry();

            var endpoints = ((IEndpointRouteBuilder)app).DataSources
                .SelectMany(source => source.Endpoints)
                .OfType<RouteEndpoint>()
                .Where(candidate => candidate.RoutePattern.RawText == LiveSessionRoutes.AdminDriverTrackerGeometryPath)
                .ToList();

            Assert.Equal(2, endpoints.Count);

            var getEndpoint = endpoints.Single(candidate => candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Get, StringComparer.OrdinalIgnoreCase) == true);
            var deleteEndpoint = endpoints.Single(candidate => candidate.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(HttpMethods.Delete, StringComparer.OrdinalIgnoreCase) == true);

            Assert.NotEmpty(getEndpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
            Assert.NotEmpty(deleteEndpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Manual recording requests return not found when the requested track is absent from the catalog.
    /// </summary>
    [Fact]
    public async Task StartRecordingReturnsNotFoundForUnknownTrack()
    {
        var recorder = CreateRecorder(out var tempRoot);
        try
        {
            var result = await InvokeStartDriverTrackerRecordingAsync(new DriverTrackerRecordingRequest
            {
                TrackName = "Unknown Track",
                TargetCompletedLaps = 2,
                ResetExistingGeometry = true,
            }, recorder, CancellationToken.None);

            Assert.Equal(StatusCodes.Status404NotFound, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            Assert.Empty(recorder.ListTracks());
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    /// <summary>
    /// Manual recording starts for known catalog tracks and preserves canonical catalog casing.
    /// </summary>
    [Fact]
    public async Task StartRecordingReturnsOkForKnownCatalogTrack()
    {
        var recorder = CreateRecorder(out var tempRoot);
        try
        {
            await recorder.StartRecordingAsync(new TrackGeometryRecordingRequest
            {
                TrackName = "Loch Drummond - Short",
                TargetCompletedLaps = 1,
                ResetExistingGeometry = true,
            }, CancellationToken.None);

            var result = await InvokeStartDriverTrackerRecordingAsync(new DriverTrackerRecordingRequest
            {
                TrackName = "loch drummond - short",
                TargetCompletedLaps = 3,
                ResetExistingGeometry = false,
            }, recorder, CancellationToken.None);

            Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
            var entry = Assert.IsType<TrackGeometryCatalogEntry>(Assert.IsAssignableFrom<IValueHttpResult>(result).Value);
            Assert.Equal("Loch Drummond - Short", entry.TrackName);
            Assert.Equal(3, entry.TargetCompletedLaps);
        }
        finally
        {
            DeleteTempRoot(tempRoot);
        }
    }

    private static Task<IResult> InvokeStartDriverTrackerRecordingAsync(
        DriverTrackerRecordingRequest request,
        TrackGeometryRecorder recorder,
        CancellationToken cancellationToken)
    {
        var method = typeof(TracksideApiEndpoints).GetMethod("StartDriverTrackerRecordingAsync", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        var task = method.Invoke(null, [request, recorder, cancellationToken]) as Task<IResult>;
        Assert.NotNull(task);
        return task;
    }

    private static TrackGeometryRecorder CreateRecorder(out string tempRoot)
    {
        tempRoot = Path.Combine(Path.GetTempPath(), $"trackside-selected-geometry-{Guid.NewGuid():N}");
        return new TrackGeometryRecorder(
            TimeProvider.System,
            new TracksideRuntimeContext(true, false, tempRoot, null),
            new StaticOptionsMonitor<TracksideOptions>(new TracksideOptions
            {
                Deployment = new TracksideDeploymentOptions { DataPath = tempRoot },
            }),
            NoopLiveDataPublisher.Instance);
    }

    private static void DeleteTempRoot(string tempRoot)
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }

    private sealed class NoopLiveDataPublisher : ILiveDataPublisher
    {
        public static NoopLiveDataPublisher Instance { get; } = new();

        public ValueTask PublishAsync<TFrame>(TFrame frame, CancellationToken cancellationToken)
            where TFrame : notnull => ValueTask.CompletedTask;
    }
}
