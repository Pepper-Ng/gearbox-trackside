using Trackside.Service.Security;

namespace Trackside.Tests;

/// <summary>
/// Covers local admin password hashing behavior.
/// </summary>
public sealed class AdminPasswordHasherTests
{
    /// <summary>
    /// Valid passwords verify against their salted PBKDF2 hash.
    /// </summary>
    [Fact]
    public void HashedPasswordVerifies()
    {
        var hash = AdminPasswordHasher.Hash("correct horse battery staple");

        Assert.Equal(AdminPasswordHasher.Algorithm, hash.Algorithm);
        Assert.Equal(AdminPasswordHasher.Iterations, hash.Iterations);
        Assert.True(AdminPasswordHasher.Verify("correct horse battery staple", hash));
        Assert.False(AdminPasswordHasher.Verify("wrong horse battery staple", hash));
    }

    /// <summary>
    /// Hashing the same password twice uses different salts.
    /// </summary>
    [Fact]
    public void HashUsesRandomSalt()
    {
        var first = AdminPasswordHasher.Hash("correct horse battery staple");
        var second = AdminPasswordHasher.Hash("correct horse battery staple");

        Assert.NotEqual(first.Salt, second.Salt);
        Assert.NotEqual(first.Hash, second.Hash);
    }
}