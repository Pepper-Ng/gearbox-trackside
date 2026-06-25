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
    public string FixturePath { get; init; } = "Fixtures/scoring-leaderboard-practice.json";

    /// <summary>
    /// Temporary staff/display aliases keyed by fixed rFactor 2 rig names.
    /// </summary>
    public Dictionary<string, string> DriverAliases { get; init; } = [];

    /// <summary>
    /// Shared-memory source configuration used when <see cref="Mode" /> is <see cref="LiveSessionSourceMode.SharedMemory" />.
    /// </summary>
    public TracksideSharedMemoryOptions SharedMemory { get; init; } = new();
}

/// <summary>
/// Shared-memory map and polling settings for future live rFactor 2 reads.
/// </summary>
public sealed class TracksideSharedMemoryOptions
{
    /// <summary>
    /// Explicit scoring map name. When omitted, common rF2 client and dedicated-server names are probed.
    /// </summary>
    public string? ScoringMapName { get; init; }

    /// <summary>
    /// Dedicated server process id used to derive PID-suffixed map names.
    /// </summary>
    public int? ProcessId { get; init; }

    /// <summary>
    /// True when Trackside should scan visible Windows shared-memory sections and known dedicated-server process names.
    /// </summary>
    public bool AutoDiscover { get; init; } = true;

    /// <summary>
    /// Process names used to derive PID-suffixed dedicated-server map names during auto-discovery.
    /// </summary>
    public string[] DedicatedServerProcessNames { get; init; } = ["rFactor2 Dedicated.exe", "Dedicated.exe"];

    /// <summary>
    /// Policy used when more than one PID-suffixed rFactor 2 scoring map is visible.
    /// </summary>
    public TracksideMultipleScoringMapPolicy MultipleScoringMapPolicy { get; init; } = TracksideMultipleScoringMapPolicy.RequireExplicitSelection;

    /// <summary>
    /// Scoring polling rate in hertz. Scoring includes leaderboard, weather, and flag channels.
    /// </summary>
    [Range(0.25, 60.0)]
    public double ScoringPollHz { get; init; } = 10.0;

    /// <summary>
    /// Telemetry loop scaffold. Leave disabled until telemetry reports or rig-local collection are active.
    /// </summary>
    public TracksideTelemetryLoopOptions Telemetry { get; init; } = new();
}

/// <summary>
/// Toggleable telemetry polling-loop settings. The loop is intentionally disabled for Phase 1 leaderboard work.
/// </summary>
public sealed class TracksideTelemetryLoopOptions
{
    /// <summary>
    /// True when the high-rate telemetry loop should run in this process.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Target telemetry polling rate in hertz for the future collector loop.
    /// </summary>
    [Range(1.0, 200.0)]
    public double PollHz { get; init; } = 100.0;
}

/// <summary>
/// Behavior when auto-discovery finds multiple dedicated-server scoring maps at once.
/// </summary>
public enum TracksideMultipleScoringMapPolicy
{
    /// <summary>
    /// Report ambiguity and require an explicit map name or process id before reading live data.
    /// </summary>
    RequireExplicitSelection,

    /// <summary>
    /// Use the first discovered scoring map. Intended only for diagnostics or temporary local development.
    /// </summary>
    UseFirstDiscovered,
}