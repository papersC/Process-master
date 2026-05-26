using System.Security.Cryptography;

namespace ESEMS.Web.Helpers;

/// <summary>
/// Provides password hashing utilities using PBKDF2 with salt.
/// Supports legacy SHA256 verification for migration.
/// </summary>
public static class PasswordHelper
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA512;

    /// <summary>
    /// Hashes a password using PBKDF2 with a random salt.
    /// Format: base64(salt + hash)
    /// </summary>
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        var result = new byte[SaltSize + HashSize];
        salt.CopyTo(result, 0);
        hash.CopyTo(result, SaltSize);
        return Convert.ToBase64String(result);
    }

    /// <summary>
    /// Verifies a password against a hash. Supports both PBKDF2 and legacy SHA256.
    /// </summary>
    public static bool Verify(string password, string storedHash)
    {
        var bytes = Convert.FromBase64String(storedHash);

        // Legacy SHA256 hash is 32 bytes (no salt)
        if (bytes.Length == 32)
            return VerifyLegacySha256(password, storedHash);

        // PBKDF2 hash is SaltSize + HashSize bytes
        if (bytes.Length != SaltSize + HashSize)
            return false;

        var salt = bytes[..SaltSize];
        var storedHashBytes = bytes[SaltSize..];
        var computedHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, Algorithm, HashSize);

        return CryptographicOperations.FixedTimeEquals(computedHash, storedHashBytes);
    }

    /// <summary>
    /// Legacy SHA256 verification for migration period.
    /// </summary>
    private static bool VerifyLegacySha256(string password, string storedHash)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
        var computed = Convert.ToBase64String(bytes);
        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(computed),
            System.Text.Encoding.UTF8.GetBytes(storedHash));
    }
}
