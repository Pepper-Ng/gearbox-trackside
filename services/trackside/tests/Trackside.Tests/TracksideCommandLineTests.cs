using Trackside.Host.Hosting;

namespace Trackside.Tests;

/// <summary>
/// Verifies the friendly Trackside command-line aliases documented in ADR-0001.
/// </summary>
public sealed class TracksideCommandLineTests
{
    /// <summary>
    /// Maps fixture source arguments to ASP.NET Core configuration keys.
    /// </summary>
    [Fact]
    public void NormalizeMapsFixtureArguments()
    {
        var normalized = TracksideCommandLine.Normalize(["--source", "fixture", "--fixture", "fixture.json", "--no-tray"]);

        Assert.Contains("--Trackside:Source:Mode=fixture", normalized);
        Assert.Contains("--Trackside:Source:FixturePath=fixture.json", normalized);
        Assert.Contains("--Trackside:Tray:Enabled=false", normalized);
    }

    /// <summary>
    /// Leaves normal ASP.NET Core configuration arguments untouched.
    /// </summary>
    [Fact]
    public void NormalizeKeepsAdvancedArguments()
    {
        var normalized = TracksideCommandLine.Normalize(["--Trackside:LiveSession:PublishIntervalSeconds=2"]);

        Assert.Equal(["--Trackside:LiveSession:PublishIntervalSeconds=2"], normalized);
    }
}