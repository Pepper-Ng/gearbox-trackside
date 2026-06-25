using Trackside.Service.Hosting;

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
        var normalized = TracksideCommandLine.Normalize(["--source", "fixture", "--fixture", "fixture.json", "--console"], out var forceConsoleMode);

        Assert.Contains("--Trackside:Source:Mode=fixture", normalized);
        Assert.Contains("--Trackside:Source:FixturePath=fixture.json", normalized);
        Assert.True(forceConsoleMode);
        Assert.DoesNotContain("--console", normalized);
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

    /// <summary>
    /// Maps packaged-runtime arguments to deployment and update configuration keys.
    /// </summary>
    [Fact]
    public void NormalizeMapsDeploymentArguments()
    {
        var normalized = TracksideCommandLine.Normalize([
            "--config-root", "C:\\Trackside\\config",
            "--install-mode", "Service",
            "--install-root", "C:\\Trackside",
            "--data-path", "C:\\Trackside\\data",
            "--logs-path", "C:\\Trackside\\logs",
            "--updates-path", "C:\\Trackside\\updates",
            "--bundle-version", "0.1.0-dev",
            "--manifest-path", "C:\\Trackside\\manifest.json",
            "--service-name", "TracksideVenue",
            "--update-candidate-manifest", "C:\\Trackside\\updates\\candidate.json",
        ], out _, out var configRoot);

        Assert.Equal("C:\\Trackside\\config", configRoot);
        Assert.Contains("--Trackside:Deployment:ConfigPath=C:\\Trackside\\config", normalized);
        Assert.Contains("--Trackside:Deployment:InstallMode=Service", normalized);
        Assert.Contains("--Trackside:Deployment:InstallRoot=C:\\Trackside", normalized);
        Assert.Contains("--Trackside:Deployment:DataPath=C:\\Trackside\\data", normalized);
        Assert.Contains("--Trackside:Deployment:LogsPath=C:\\Trackside\\logs", normalized);
        Assert.Contains("--Trackside:Deployment:UpdatesPath=C:\\Trackside\\updates", normalized);
        Assert.Contains("--Trackside:Deployment:BundleVersion=0.1.0-dev", normalized);
        Assert.Contains("--Trackside:Deployment:ManifestPath=C:\\Trackside\\manifest.json", normalized);
        Assert.Contains("--Trackside:Deployment:ServiceName=TracksideVenue", normalized);
        Assert.Contains("--Trackside:Updates:CandidateManifestPath=C:\\Trackside\\updates\\candidate.json", normalized);
    }
}