using System.Text.Json;
using System.Text.Json.Nodes;
using Trackside.Application.Serialization;
using Trackside.Service.Hosting;

namespace Trackside.Service.Configuration;

/// <summary>
/// Persists operator-editable non-source Trackside configuration to the writable appsettings override file.
/// </summary>
public sealed class TracksideWritableConfigurationStore
{
    private readonly TracksideRuntimeContext _runtimeContext;

    /// <summary>
    /// Creates the writable configuration store.
    /// </summary>
    /// <param name="runtimeContext">Runtime paths used to locate writable configuration.</param>
    public TracksideWritableConfigurationStore(TracksideRuntimeContext runtimeContext)
    {
        _runtimeContext = runtimeContext;
    }

    /// <summary>
    /// Override file path used by writable admin settings.
    /// </summary>
    public string WritableConfigurationPath => !string.IsNullOrWhiteSpace(_runtimeContext.ExternalConfigRoot)
        ? Path.Combine(_runtimeContext.ExternalConfigRoot, "service", "appsettings.json")
        : Path.Combine(_runtimeContext.ContentRootPath, "appsettings.Local.json");

    /// <summary>
    /// Persists kiosk display defaults.
    /// </summary>
    /// <param name="kiosk">Kiosk options to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes after the file is written.</returns>
    public async Task SaveKioskAsync(TracksideKioskOptions kiosk, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kiosk);
        var path = WritableConfigurationPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _runtimeContext.ContentRootPath);

        var root = await LoadConfigurationRootAsync(path, cancellationToken);
        var trackside = GetOrCreateObject(root, "Trackside");
        trackside["Kiosk"] = JsonSerializer.SerializeToNode(kiosk, TracksideJson.SerializerOptions);

        var writeOptions = new JsonSerializerOptions(TracksideJson.SerializerOptions)
        {
            WriteIndented = true,
        };
        await WriteAllTextAtomicallyAsync(path, root.ToJsonString(writeOptions), cancellationToken);
    }

    /// <summary>
    /// Persists frontend localization settings.
    /// </summary>
    /// <param name="localization">Localization options to persist.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Task that completes after the file is written.</returns>
    public async Task SaveLocalizationAsync(TracksideLocalizationOptions localization, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(localization);
        var path = WritableConfigurationPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? _runtimeContext.ContentRootPath);

        var root = await LoadConfigurationRootAsync(path, cancellationToken);
        var trackside = GetOrCreateObject(root, "Trackside");
        trackside["Localization"] = JsonSerializer.SerializeToNode(localization, TracksideJson.SerializerOptions);

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
}