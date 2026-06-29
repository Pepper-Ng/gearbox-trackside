using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Trackside.Application.LiveSession;
using Trackside.Service.LiveData;

namespace Trackside.Tests;

/// <summary>
/// Covers live-data fan-out behavior used by telemetry and scoring modules.
/// </summary>
public sealed class LiveDataPublisherTests
{
    /// <summary>
    /// One failing consumer does not prevent other consumers from receiving the projected frame.
    /// </summary>
    [Fact]
    public async Task PublishContinuesAfterConsumerFailure()
    {
        var recordingConsumer = new RecordingConsumer();
        var services = new ServiceCollection()
            .AddSingleton<ILiveDataConsumer<TestFrame>, ThrowingConsumer>()
            .AddSingleton<ILiveDataConsumer<TestFrame>>(recordingConsumer)
            .BuildServiceProvider();
        var publisher = new LiveDataPublisher(services, NullLogger<LiveDataPublisher>.Instance);

        await publisher.PublishAsync(new TestFrame("telemetry"), CancellationToken.None);

        Assert.Equal("telemetry", recordingConsumer.LastFrame?.Name);
    }

    /// <summary>
    /// A pre-canceled publish stops before invoking consumers and does not throw during normal shutdown.
    /// </summary>
    [Fact]
    public async Task PublishStopsQuietlyWhenTokenAlreadyCanceled()
    {
        var recordingConsumer = new RecordingConsumer();
        var services = new ServiceCollection()
            .AddSingleton<ILiveDataConsumer<TestFrame>>(recordingConsumer)
            .BuildServiceProvider();
        var publisher = new LiveDataPublisher(services, NullLogger<LiveDataPublisher>.Instance);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await publisher.PublishAsync(new TestFrame("telemetry"), cancellation.Token);

        Assert.Null(recordingConsumer.LastFrame);
    }

    /// <summary>
    /// A consumer-local cancellation is treated like a skipped consumer, not a failed publishing cycle.
    /// </summary>
    [Fact]
    public async Task PublishContinuesAfterConsumerLocalCancellation()
    {
        var recordingConsumer = new RecordingConsumer();
        var services = new ServiceCollection()
            .AddSingleton<ILiveDataConsumer<TestFrame>, CancelingConsumer>()
            .AddSingleton<ILiveDataConsumer<TestFrame>>(recordingConsumer)
            .BuildServiceProvider();
        var publisher = new LiveDataPublisher(services, NullLogger<LiveDataPublisher>.Instance);

        await publisher.PublishAsync(new TestFrame("telemetry"), CancellationToken.None);

        Assert.Equal("telemetry", recordingConsumer.LastFrame?.Name);
    }

    private sealed record TestFrame(string Name);

    private sealed class ThrowingConsumer : ILiveDataConsumer<TestFrame>
    {
        public ValueTask ConsumeAsync(TestFrame frame, CancellationToken cancellationToken) => throw new InvalidOperationException("boom");
    }

    private sealed class CancelingConsumer : ILiveDataConsumer<TestFrame>
    {
        public ValueTask ConsumeAsync(TestFrame frame, CancellationToken cancellationToken) => throw new OperationCanceledException("consumer canceled");
    }

    private sealed class RecordingConsumer : ILiveDataConsumer<TestFrame>
    {
        public TestFrame? LastFrame { get; private set; }

        public ValueTask ConsumeAsync(TestFrame frame, CancellationToken cancellationToken)
        {
            LastFrame = frame;
            return ValueTask.CompletedTask;
        }
    }
}