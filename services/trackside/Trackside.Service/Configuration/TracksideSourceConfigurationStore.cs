using System.Text.Json;
using System.Text.Json.Nodes;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Application.Serialization;
using Trackside.Service.Hosting;

namespace Trackside.Service.Configuration;

/// <summary>
/// Persists operator-editable source configuration to the writable appsettings override file.
/// </summary>
public sealed class TracksideSourceConfigurationStore
{
    private readonly TracksideRuntimeContext _runtimeContext;

    /// <summary>
    /// Creates the source configuration store.
    /// </summary>
    /// <param name="runtimeContext">Runtime paths used to locate writable configuration.</param>
    public TracksideSourceConfigurationStore(TracksideRuntimeContext runtimeContext)
    {
        _runtimeContext = runtimeContext;
    }

    /// <summary>
    /// Override file path used by the configuration page.
    /// </summary>
    public string WritableConfigurationPath => !string.IsNullOrWhiteSpace(_runtimeContext.ExternalConfigRoot)
        ? Path.Combine(_runtimeContext.ExternalConfigRoot, "service", "appsettings.json")
        : Path.Combine(_runtimeContext.ContentRootPath, "appsettings.Local.json");

    /// <summary>
    /// Persists a source configuration override.
    /// </summary>
    /// <param name="request">Source configuration values to write.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="includeDriverAliases">True when aliases should remain in the source configuration file.</param>
    /// <returns>Task that completes after the file is written.</returns>
    public async Task SaveAsync(SourceConfigurationRequest request, CancellationToken cancellationToken, bool includeDriverAliases = true)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        var path = WritableConfigurationPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _runtimeContext.ContentRootPath);

        var sourceOptions = new TracksideSourceOptions
        {
            Mode = request.Mode,
            FixturePath = request.FixturePath,
            DriverAliases = includeDriverAliases ? request.DriverAliases : [],
            SharedMemory = request.SharedMemory,
        };

        var root = await LoadConfigurationRootAsync(path, cancellationToken);
        var trackside = GetOrCreateObject(root, "Trackside");
        trackside["Source"] = JsonSerializer.SerializeToNode(sourceOptions, TracksideJson.SerializerOptions);

        var writeOptions = new JsonSerializerOptions(TracksideJson.SerializerOptions)
        {
            WriteIndented = true,
        };
        await WriteAllTextAtomicallyAsync(path, root.ToJsonString(writeOptions), cancellationToken);
    }

    private static async Task<JsonObject> LoadConfigurationRootAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        await using var stream = File.OpenRead(path);
        var root = await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken);
        return root as JsonObject ?? throw new InvalidDataException($"Configuration file must contain a JSON object: {path}");
    }

    private static JsonObject GetOrCreateObject(JsonObject root, string propertyName)
    {
        if (root[propertyName] is JsonObject child)
        {
            return child;
        }

        child = [];
        root[propertyName] = child;
        return child;
    }

    private static async Task WriteAllTextAtomicallyAsync(string path, string content, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
        File.Move(temporaryPath, path, overwrite: true);
    }

    private static void Validate(SourceConfigurationRequest request)
    {
        if (request.Mode == LiveSessionSourceMode.Recorded)
        {
            throw new ArgumentException("Recorded source mode is not implemented in the Phase 1 admin dashboard.");
        }

        if (request.Mode == LiveSessionSourceMode.Fixture && string.IsNullOrWhiteSpace(request.FixturePath))
        {
            throw new ArgumentException("Fixture mode requires a fixture path.");
        }

        if (request.SharedMemory.ProcessId is < 0)
        {
            throw new ArgumentException("Shared-memory process id cannot be negative.");
        }

        if (request.SharedMemory.ScoringPollHz is < 0.25 or > 60.0)
        {
            throw new ArgumentException("Shared-memory scoring poll Hz must be between 0.25 and 60.");
        }

        if (request.SharedMemory.Telemetry.PollHz is < 1.0 or > 200.0)
        {
            throw new ArgumentException("Telemetry poll Hz must be between 1 and 200.");
        }

        if (request.SharedMemory.DedicatedServerProcessNames.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Dedicated server process names cannot contain blank values.");
        }
    }
}

/// <summary>
/// Source configuration payload returned to the configuration page.
/// </summary>
public sealed record SourceConfigurationResponse
{
    /// <summary>
    /// Current source mode.
    /// </summary>
    public LiveSessionSourceMode Mode { get; init; }

    /// <summary>
    /// Current fixture path.
    /// </summary>
    public string FixturePath { get; init; } = string.Empty;

    /// <summary>
    /// Current rig-name aliases.
    /// </summary>
    public Dictionary<string, string> DriverAliases { get; init; } = [];

    /// <summary>
    /// Shared-memory discovery and polling options.
    /// </summary>
    public TracksideSharedMemoryOptions SharedMemory { get; init; } = new();

    /// <summary>
    /// Writable override file used by PUT requests.
    /// </summary>
    public string WritableConfigurationPath { get; init; } = string.Empty;

    /// <summary>
    /// Current discovery result for the configured shared-memory settings.
    /// </summary>
    public SharedMemoryDiscoveryResponse Discovery { get; init; } = new();
}

/// <summary>
/// Source configuration payload accepted from the configuration page.
/// </summary>
public sealed record SourceConfigurationRequest
{
    /// <summary>
    /// Desired source mode.
    /// </summary>
    public LiveSessionSourceMode Mode { get; init; } = LiveSessionSourceMode.Fixture;

    /// <summary>
    /// Fixture path used in fixture mode.
    /// </summary>
    public string FixturePath { get; init; } = "Fixtures/scoring-leaderboard-practice.json";

    /// <summary>
    /// Rig-name aliases.
    /// </summary>
    public Dictionary<string, string> DriverAliases { get; init; } = [];

    /// <summary>
    /// Shared-memory discovery and polling options.
    /// </summary>
    public TracksideSharedMemoryOptions SharedMemory { get; init; } = new();
}

/// <summary>
/// Shared-memory discovery status returned to the configuration page.
/// </summary>
public sealed record SharedMemoryDiscoveryResponse
{
    /// <summary>
    /// True when multiple visible maps require explicit selection.
    /// </summary>
    public bool IsAmbiguous { get; init; }

    /// <summary>
    /// Resolution status text.
    /// </summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Ordered map names that will be tried by the live source.
    /// </summary>
    public IReadOnlyList<string> CandidateMapNames { get; init; } = [];

    /// <summary>
    /// Discovered visible scoring map candidates.
    /// </summary>
    public IReadOnlyList<SharedMemoryMapCandidateResponse> DiscoveredCandidates { get; init; } = [];

    /// <summary>
    /// Ambiguous candidates when <see cref="IsAmbiguous" /> is true.
    /// </summary>
    public IReadOnlyList<SharedMemoryMapCandidateResponse> AmbiguousCandidates { get; init; } = [];
}

/// <summary>
/// One discovered shared-memory map candidate returned to the configuration page.
/// </summary>
public sealed record SharedMemoryMapCandidateResponse
{
    /// <summary>
    /// Map name that can be configured explicitly.
    /// </summary>
    public string MapName { get; init; } = string.Empty;

    /// <summary>
    /// PID suffix extracted from the map name, when present.
    /// </summary>
    public int? ProcessId { get; init; }

    /// <summary>
    /// Matched process name when known.
    /// </summary>
    public string? ProcessName { get; init; }

    /// <summary>
    /// Discovery mechanism that found the candidate.
    /// </summary>
    public string DiscoverySource { get; init; } = string.Empty;
}