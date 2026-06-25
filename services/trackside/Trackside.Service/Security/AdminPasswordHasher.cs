using System.Security.Cryptography;

namespace Trackside.Service.Security;

/// <summary>
/// PBKDF2-HMAC-SHA256 password hashing for local Trackside admin accounts.
/// </summary>
public static class AdminPasswordHasher
{
    /// <summary>
    /// Hash algorithm label persisted in the admin user store.
    /// </summary>
    public const string Algorithm = "PBKDF2-HMACSHA256";

    /// <summary>
    /// Current PBKDF2 iteration count.
    /// </summary>
    public const int Iterations = 210_000;

    private const int SaltBytes = 32;
    private const int HashBytes = 32;

    /// <summary>
    /// Hashes a password using a fresh random salt.
    /// </summary>
    /// <param name="password">Plain-text password provided by the admin.</param>
    /// <returns>Password hash record ready to persist.</returns>
    public static AdminPasswordHash Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return new AdminPasswordHash(
            Algorithm,
            Iterations,
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Verifies a password against a persisted hash.
    /// </summary>
    /// <param name="password">Plain-text password provided by the admin.</param>
    /// <param name="hash">Persisted password hash.</param>
    /// <returns>True when the password matches.</returns>
    public static bool Verify(string password, AdminPasswordHash hash)
    {
        if (!string.Equals(hash.Algorithm, Algorithm, StringComparison.Ordinal) || hash.Iterations <= 0)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(hash.Salt);
            var expectedHash = Convert.FromBase64String(hash.Hash);
            var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, hash.Iterations, HashAlgorithmName.SHA256, expectedHash.Length);
            return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

/// <summary>
/// Persisted password hash details.
/// </summary>
/// <param name="Algorithm">Hash algorithm label.</param>
/// <param name="Iterations">PBKDF2 iteration count.</param>
/// <param name="Salt">Base64 salt.</param>
/// <param name="Hash">Base64 hash.</param>
public sealed record AdminPasswordHash(string Algorithm, int Iterations, string Salt, string Hash);