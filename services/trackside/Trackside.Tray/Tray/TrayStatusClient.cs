using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Trackside.Tray.Tray;

/// <summary>
/// Reads Trackside service status for the tray icon overlay.
/// </summary>
public interface ITrayStatusClient
{
    /// <summary>
    /// Reads and classifies the current tray status.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for shutdown.</param>
    /// <returns>Current tray connection status.</returns>
    Task<TrayConnectionStatus> GetStatusAsync(CancellationToken cancellationToken);
}

/// <summary>
/// HTTP implementation that reads the current live-session endpoint.
/// </summary>
public sealed class TrayStatusClient : ITrayStatusClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly TracksideTrayOptions _options;
    private readonly ILogger<TrayStatusClient> _logger;

    /// <summary>
    /// Creates the status client.
    /// </summary>
    /// <param name="httpClient">HTTP client used for local service requests.</param>
    /// <param name="options">Tray options with host URL and timeout settings.</param>
    /// <param name="logger">Logger for status request failures.</param>
    public TrayStatusClient(
        HttpClient httpClient,
        IOptions<TracksideTrayOptions> options,
        ILogger<TrayStatusClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TrayConnectionStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(0.25, _options.StatusRequestTimeoutSeconds)));

            var snapshot = await _httpClient.GetFromJsonAsync<LiveSessionStatusResponse>(
                CombineBaseUrl(_options.HostBaseUrl, "/api/live-session/current"),
                JsonOptions,
                timeout.Token);

            return TrayStatusClassifier.Classify(snapshot?.ToObservation());
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TrayConnectionStatus.Disconnected;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            _logger.LogDebug(ex, "Failed to read Trackside service status for tray icon.");
            return TrayConnectionStatus.Disconnected;
        }
    }

    private static string CombineBaseUrl(string baseUrl, string route)
    {
        var normalizedRoute = route.StartsWith('/') ? route : "/" + route;
        return baseUrl.TrimEnd('/') + normalizedRoute;
    }

    private sealed record LiveSessionStatusResponse(string? Source, string? Status, LiveSessionStatusSession? Session)
    {
        public TrayStatusObservation ToObservation() => new(Source, Status, Session?.ToObservation());
    }

    private sealed record LiveSessionStatusSession(string? Kind, string? Phase)
    {
        public TraySessionObservation ToObservation() => new(Kind, Phase);
    }
}