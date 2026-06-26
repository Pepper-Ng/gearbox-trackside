using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Trackside.Application.Persistence;
using Trackside.Service.Configuration;

namespace Trackside.Service.Workers;

/// <summary>
/// Initializes durable Phase 2 persistence before live publishing starts.
/// </summary>
public sealed class TracksidePersistenceInitializer : IHostedService
{
    private readonly ITracksideStore _store;
    private readonly IOptionsMonitor<TracksideOptions> _options;
    private readonly ILogger<TracksidePersistenceInitializer> _logger;

    /// <summary>
    /// Creates the persistence initializer.
    /// </summary>
    /// <param name="store">Durable Trackside store.</param>
    /// <param name="options">Current Trackside options.</param>
    /// <param name="logger">Logger for startup status.</param>
    public TracksidePersistenceInitializer(
        ITracksideStore store,
        IOptionsMonitor<TracksideOptions> options,
        ILogger<TracksidePersistenceInitializer> logger)
    {
        _store = store;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _store.InitializeAsync(cancellationToken);
        await _store.SeedDriverAliasesAsync(_options.CurrentValue.Source.DriverAliases, cancellationToken);
        if (_store.IsEnabled)
        {
            _logger.LogInformation(
                "Trackside persistence initialized using {Provider} at {Location}.",
                _store.Status.Provider,
                _store.Status.DisplayLocation);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}