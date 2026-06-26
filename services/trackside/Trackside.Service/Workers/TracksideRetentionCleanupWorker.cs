using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trackside.Application.Persistence;
using Trackside.Service.Configuration;

namespace Trackside.Service.Workers;

/// <summary>
/// Periodically enforces configured persistence retention windows.
/// </summary>
public sealed class TracksideRetentionCleanupWorker : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(24);
    private readonly ITracksideStore _store;
    private readonly IOptionsMonitor<TracksideOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TracksideRetentionCleanupWorker> _logger;

    /// <summary>
    /// Creates the retention cleanup worker.
    /// </summary>
    /// <param name="store">Durable Trackside store.</param>
    /// <param name="options">Current Trackside options.</param>
    /// <param name="timeProvider">Clock used for retention cutoffs.</param>
    /// <param name="logger">Logger for cleanup results.</param>
    public TracksideRetentionCleanupWorker(
        ITracksideStore store,
        IOptionsMonitor<TracksideOptions> options,
        TimeProvider timeProvider,
        ILogger<TracksideRetentionCleanupWorker> logger)
    {
        _store = store;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCleanupAsync(stoppingToken);
        using var timer = new PeriodicTimer(CleanupInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RunCleanupAsync(stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        if (!_store.IsEnabled)
        {
            return;
        }

        try
        {
            var result = await _store.EnforceRetentionAsync(
                _options.CurrentValue.Persistence.Retention,
                _timeProvider.GetUtcNow(),
                cancellationToken);
            if (result.DetailedLapRecordsDeleted > 0 || result.SessionSummariesDeleted > 0 || result.TrackBestRecordsDeleted > 0 || result.MonthlyTrackPeriodsDeleted > 0)
            {
                _logger.LogInformation(
                    "Trackside retention cleanup deleted {LapCount} lap rows, {SessionCount} session rows, {TrackBestCount} track-best rows, and {MonthlyTrackCount} monthly track rows.",
                    result.DetailedLapRecordsDeleted,
                    result.SessionSummariesDeleted,
                    result.TrackBestRecordsDeleted,
                    result.MonthlyTrackPeriodsDeleted);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trackside retention cleanup failed.");
        }
    }
}