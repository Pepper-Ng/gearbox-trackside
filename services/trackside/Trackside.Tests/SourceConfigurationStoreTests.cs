using System.Text.Json.Nodes;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Service.Configuration;
using Trackside.Service.Hosting;

namespace Trackside.Tests;

/// <summary>
/// Covers writable source configuration persistence.
/// </summary>
public sealed class SourceConfigurationStoreTests
{
    /// <summary>
    /// Saving source configuration preserves unrelated settings in the same appsettings file.
    /// </summary>
    [Fact]
    public async Task SaveAsyncMergesSourceSectionIntoExistingConfiguration()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var configPath = Path.Combine(temporaryDirectory.Path, "appsettings.Local.json");
        await File.WriteAllTextAsync(
            configPath,
            """
            {
              "Trackside": {
                "Http": {
                  "ListenUrl": "http://127.0.0.1:9999"
                },
                "Deployment": {
                  "InstallMode": "Existing"
                }
              }
            }
            """);
        var store = new TracksideSourceConfigurationStore(new TracksideRuntimeContext(false, false, temporaryDirectory.Path, null));

        await store.SaveAsync(new SourceConfigurationRequest
        {
            Mode = LiveSessionSourceMode.SharedMemory,
            FixturePath = "Fixtures/scoring-leaderboard-practice.json",
            DriverAliases = new Dictionary<string, string> { ["Setup1"] = "Maya" },
            SharedMemory = new TracksideSharedMemoryOptions
            {
                AutoDiscover = true,
                ScoringPollHz = 10.0,
            },
        }, CancellationToken.None);

        var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!.AsObject();

        Assert.Equal("http://127.0.0.1:9999", root["Trackside"]?["Http"]?["ListenUrl"]?.GetValue<string>());
        Assert.Equal("Existing", root["Trackside"]?["Deployment"]?["InstallMode"]?.GetValue<string>());
        Assert.Equal("SharedMemory", root["Trackside"]?["Source"]?["mode"]?.GetValue<string>());
        Assert.Equal("Maya", root["Trackside"]?["Source"]?["driverAliases"]?["Setup1"]?.GetValue<string>());
    }

        /// <summary>
        /// Saving kiosk settings preserves unrelated settings in the writable appsettings file.
        /// </summary>
        [Fact]
        public async Task SaveKioskAsyncMergesKioskSectionIntoExistingConfiguration()
        {
                using var temporaryDirectory = new TemporaryDirectory();
                var configPath = Path.Combine(temporaryDirectory.Path, "appsettings.Local.json");
                await File.WriteAllTextAsync(
                        configPath,
                        """
                        {
                            "Trackside": {
                                "Http": {
                                    "ListenUrl": "http://127.0.0.1:9999"
                                }
                            }
                        }
                        """);
                var store = new TracksideWritableConfigurationStore(new TracksideRuntimeContext(false, false, temporaryDirectory.Path, null));

                await store.SaveKioskAsync(new TracksideKioskOptions { DefaultDisplayMode = KioskDisplayMode.Live }, CancellationToken.None);

                var root = JsonNode.Parse(await File.ReadAllTextAsync(configPath))!.AsObject();

                Assert.Equal("http://127.0.0.1:9999", root["Trackside"]?["Http"]?["ListenUrl"]?.GetValue<string>());
                Assert.Equal("Live", root["Trackside"]?["Kiosk"]?["defaultDisplayMode"]?.GetValue<string>());
        }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"trackside-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}