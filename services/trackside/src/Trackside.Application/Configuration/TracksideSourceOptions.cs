using System.ComponentModel.DataAnnotations;
using Trackside.Application.LiveSession;

namespace Trackside.Application.Configuration;

/// <summary>
/// Live-session source options used by the source factory and source implementations.
/// </summary>
public sealed class TracksideSourceOptions
{
    /// <summary>
    /// Selected live-session source mode. Phase 0B defaults to fixture mode.
    /// </summary>
    public LiveSessionSourceMode Mode { get; init; } = LiveSessionSourceMode.Fixture;

    /// <summary>
    /// Fixture path used when <see cref="Mode" /> is <see cref="LiveSessionSourceMode.Fixture" />.
    /// </summary>
    [Required]
    public string FixturePath { get; init; } = "Fixtures/mock-live-session.json";
}