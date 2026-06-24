using Microsoft.Extensions.Logging;

namespace Trackside.Infrastructure.Rf2.SharedMemory;

/// <summary>
/// Owns a dedicated polling loop for one subscribed shared-memory stream.
/// </summary>
/// <typeparam name="T">Latest value type produced by the loop.</typeparam>
public sealed class SharedMemoryPollingLoop<T> : IDisposable
    where T : class
{
    private readonly string _name;
    private readonly bool _enabled;
    private readonly TimeSpan _pollInterval;
    private readonly Func<CancellationToken, Task<T?>> _readAsync;
    private readonly ILogger _logger;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task? _worker;
    private T? _latest;
    private Exception? _lastError;
    private DateTimeOffset? _lastSuccessUtc;

    /// <summary>
    /// Creates and starts the polling loop when enabled.
    /// </summary>
    /// <param name="name">Diagnostic stream name.</param>
    /// <param name="enabled">True when the loop should run.</param>
    /// <param name="pollInterval">Delay between reads.</param>
    /// <param name="readAsync">Read callback for one poll iteration.</param>
    /// <param name="logger">Logger for loop failures.</param>
    public SharedMemoryPollingLoop(
        string name,
        bool enabled,
        TimeSpan pollInterval,
        Func<CancellationToken, Task<T?>> readAsync,
        ILogger logger)
    {
        _name = name;
        _enabled = enabled;
        _pollInterval = pollInterval;
        _readAsync = readAsync;
        _logger = logger;

        if (_enabled)
        {
            _worker = Task.Run(RunAsync);
        }
    }

    /// <summary>
    /// Latest successfully read value.
    /// </summary>
    public T? Latest => Volatile.Read(ref _latest);

    /// <summary>
    /// Human-readable loop status for health and source diagnostics.
    /// </summary>
    public string Status
    {
        get
        {
            if (!_enabled)
            {
                return $"{_name} loop disabled";
            }

            if (_lastError is not null)
            {
                return $"{_name} loop waiting: {_lastError.Message}";
            }

            return _lastSuccessUtc is not null
                ? $"{_name} loop last read at {_lastSuccessUtc:O}"
                : $"{_name} loop waiting for first read";
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        try
        {
            _worker?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _stop.Dispose();
    }

    private async Task RunAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                var value = await _readAsync(_stop.Token);
                if (value is not null)
                {
                    Volatile.Write(ref _latest, value);
                    _lastSuccessUtc = DateTimeOffset.UtcNow;
                    _lastError = null;
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Volatile.Write(ref _latest, null);
                _lastError = ex;
                _logger.LogDebug(ex, "{LoopName} shared-memory polling iteration failed.", _name);
            }

            await Task.Delay(_pollInterval, _stop.Token);
        }
    }
}