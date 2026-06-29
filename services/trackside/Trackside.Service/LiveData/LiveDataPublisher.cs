using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trackside.Application.LiveSession;

namespace Trackside.Service.LiveData;

/// <summary>
/// Publishes projected live-data frames to all modules registered for the frame type.
/// </summary>
public sealed class LiveDataPublisher : ILiveDataPublisher
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LiveDataPublisher> _logger;

    /// <summary>
    /// Creates a live-data publisher.
    /// </summary>
    /// <param name="serviceProvider">Service provider used to resolve registered consumers.</param>
    /// <param name="logger">Logger for consumer failures.</param>
    public LiveDataPublisher(IServiceProvider serviceProvider, ILogger<LiveDataPublisher> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask PublishAsync<TFrame>(TFrame frame, CancellationToken cancellationToken)
        where TFrame : notnull
    {
        foreach (var consumer in _serviceProvider.GetServices<ILiveDataConsumer<TFrame>>())
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            try
            {
                await consumer.ConsumeAsync(frame, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException ex)
            {
                _logger.LogDebug(ex, "Live-data consumer {ConsumerType} canceled while handling {FrameType}.", consumer.GetType().Name, typeof(TFrame).Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Live-data consumer {ConsumerType} failed while handling {FrameType}.", consumer.GetType().Name, typeof(TFrame).Name);
            }
        }
    }
}