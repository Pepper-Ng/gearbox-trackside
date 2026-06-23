using System.Text.Json;
using System.Text.Json.Serialization;

namespace Trackside.Application.Serialization;

/// <summary>
/// Shared JSON settings for REST endpoints, SignalR payloads, fixture loading, and tests.
/// </summary>
public static class TracksideJson
{
    /// <summary>
    /// Serializer options used by fixture loaders that run outside ASP.NET Core's option pipeline.
    /// </summary>
    public static JsonSerializerOptions SerializerOptions
    {
        get
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
            Configure(options);
            return options;
        }
    }

    /// <summary>
    /// Configures JSON to use web naming and string enum values for durable browser contracts.
    /// </summary>
    /// <param name="options">Serializer options to configure.</param>
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;
        if (!options.Converters.OfType<JsonStringEnumConverter>().Any())
        {
            options.Converters.Add(new JsonStringEnumConverter());
        }
    }
}