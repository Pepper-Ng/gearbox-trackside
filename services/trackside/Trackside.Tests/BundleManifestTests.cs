using System.Security.Cryptography;
using System.Text;
using Trackside.Updater;

namespace Trackside.Tests;

/// <summary>
/// Verifies the updater manifest contract used by packaged Trackside bundles.
/// </summary>
public sealed class BundleManifestTests
{
    /// <summary>
    /// Accepts a manifest when all listed files exist and match their SHA-256 hashes.
    /// </summary>
    [Fact]
    public void VerifyAcceptsMatchingManifestFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = Path.Combine(temporaryDirectory.Path, "app", "service", "Trackside.Service.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "service", Encoding.UTF8);

        var manifest = new BundleManifest
        {
            BundleVersion = "0.1.0-test",
            Files = [new BundleManifestFile("app/service/Trackside.Service.exe", HashFile(filePath), new FileInfo(filePath).Length)],
        };

        var result = BundleManifestVerifier.Verify(manifest, temporaryDirectory.Path);

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Errors));
    }

    /// <summary>
    /// Rejects a manifest when a listed file no longer matches its checksum.
    /// </summary>
    [Fact]
    public void VerifyRejectsChecksumMismatch()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = Path.Combine(temporaryDirectory.Path, "manifested.txt");
        File.WriteAllText(filePath, "changed", Encoding.UTF8);

        var manifest = new BundleManifest
        {
            BundleVersion = "0.1.0-test",
            Files = [new BundleManifestFile("manifested.txt", new string('0', 64), new FileInfo(filePath).Length)],
        };

        var result = BundleManifestVerifier.Verify(manifest, temporaryDirectory.Path);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("SHA-256 mismatch", StringComparison.Ordinal));
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
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