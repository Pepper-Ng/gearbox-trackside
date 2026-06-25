namespace Trackside.RigAgent;

/// <summary>
/// Placeholder hosted service for the future rig-side agent.
/// </summary>
/// <param name="logger">Logger used to record lifecycle events.</param>
public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    /// <summary>
    /// Keeps the process alive without performing rig-side behavior in Phase 0B.
    /// </summary>
    /// <param name="stoppingToken">Signals host shutdown.</param>
    /// <returns>A task that completes when shutdown is requested.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Trackside rig agent scaffold started. No rig-side actions are implemented in Phase 0B.");

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Trackside rig agent scaffold stopped.");
        }
    }
}
