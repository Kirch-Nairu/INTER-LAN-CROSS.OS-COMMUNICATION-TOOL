using System.Security.Cryptography;
using System.Text;

namespace InterLan.Infrastructure;

public static class SecretCodec
{
    public const int PasswordIterations = 210_000;

    public static string NewToken(int byteLength = 32) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength)).ToLowerInvariant();

    public static string HashToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ArgumentException("Token is required.", nameof(token));

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
    }

    public static (string Salt, string Hash, int Iterations) HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 10)
            throw new ArgumentException("Password must contain at least 10 characters.", nameof(password));

        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            32);

        return (
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash),
            PasswordIterations);
    }

    public static bool VerifyPassword(string password, string saltBase64, string hashBase64, int iterations)
    {
        var salt = Convert.FromBase64String(saltBase64);
        var expected = Convert.FromBase64String(hashBase64);
        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
