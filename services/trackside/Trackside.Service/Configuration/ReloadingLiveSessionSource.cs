using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Application.Serialization;
using Trackside.Domain.LiveSession;
using Trackside.Infrastructure.LiveSession;
using Trackside.Infrastructure.LiveSession.Fixtures;
using Trackside.Infrastructure.LiveSession.SharedMemory;

namespace Trackside.Service.Configuration;

/// <summary>
/// Recreates the configured live-session source when source settings change.
/// </summary>
public sealed class ReloadingLiveSessionSource : ILiveSessionSource, IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptionsMonitor<TracksideOptions> _options;
    private readonly object _gate = new();
    private ILiveSessionSource? _currentSource;
    private string? _currentSignature;

    /// <summary>
    /// Creates the reload-aware source wrapper.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to construct concrete source adapters.</param>
    /// <param name="options">Live Trackside options.</param>
    public ReloadingLiveSessionSource(IServiceProvider serviceProvider, IOptionsMonitor<TracksideOptions> options)
    {
        _serviceProvider = serviceProvider;
        _options = options;
    }

    /// <inheritdoc />
    public Task<LiveSessionSnapshot> GetCurrentAsync(CancellationToken cancellationToken) =>
        GetCurrentSource().GetCurrentAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            DisposeSource(_currentSource);
            _currentSource = null;
            _currentSignature = null;
        }
    }

    private ILiveSessionSource GetCurrentSource()
    {
        var options = _options.CurrentValue;
        var signature = JsonSerializer.Serialize(options.Source, TracksideJson.SerializerOptions);

        lock (_gate)
        {
            if (_currentSource is not null && string.Equals(signature, _currentSignature, StringComparison.Ordinal))
            {
                return _currentSource;
            }

            var nextSource = CreateSource(options.Source);
            DisposeSource(_currentSource);
            _currentSource = nextSource;
            _currentSignature = signature;
            return nextSource;
        }
    }

    private ILiveSessionSource CreateSource(TracksideSourceOptions sourceOptions) => sourceOptions.Mode switch
    {
        LiveSessionSourceMode.Fixture => ActivatorUtilities.CreateInstance<FixtureLiveSessionSource>(
            _serviceProvider,
            sourceOptions.FixturePath,
            AppContext.BaseDirectory),
        LiveSessionSourceMode.SharedMemory => ActivatorUtilities.CreateInstance<SharedMemoryLiveSessionSource>(_serviceProvider),
        _ => ActivatorUtilities.CreateInstance<UnsupportedLiveSessionSource>(_serviceProvider, sourceOptions.Mode),
    };

    private static void DisposeSource(ILiveSessionSource? source)
    {
        if (source is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}