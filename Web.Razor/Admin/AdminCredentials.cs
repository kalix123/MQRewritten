using Microsoft.Extensions.Logging;
using Server.Base.Core.Extensions;
using System.Security.Cryptography;

namespace Web.Razor.Admin;

// MQ Rewritten: the one admin login that always works, from the ADMIN_USERNAME and ADMIN_PASSWORD
// environment variables (set in the compose .env). It isn't a game account, so bans and access levels
// don't apply, and it's exempt from the per-username lockout so nobody can lock the owner out.
// The per-IP lockout still applies, so it can't be brute-forced.
public class AdminCredentials
{
    public const int MinPasswordLength = 12;

    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName _algorithm = HashAlgorithmName.SHA512;

    private readonly byte[] _salt = RandomNumberGenerator.GetBytes(SaltSize);
    private readonly byte[] _hash;

    // Checked instead of a real hash when the username is unknown, so a miss takes as long as a hit.
    private readonly byte[] _dummySalt = RandomNumberGenerator.GetBytes(SaltSize);
    private readonly byte[] _dummyHash;

    public string Username { get; }
    public bool Enabled => Username != null;

    public AdminCredentials(ILogger<AdminCredentials> logger)
    {
        _dummyHash = Derive(RandomNumberGenerator.GetHexString(32), _dummySalt);

        var username = Environment.GetEnvironmentVariable("ADMIN_USERNAME").Sanitize();
        var password = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            logger.LogWarning("ADMIN_USERNAME / ADMIN_PASSWORD aren't set, so only owner accounts can open the admin page");
            return;
        }

        if (password.Length < MinPasswordLength)
        {
            logger.LogWarning("ADMIN_PASSWORD is shorter than {Length} characters, so the admin login is disabled", MinPasswordLength);
            return;
        }

        Username = username;
        _hash = Derive(password, _salt);
    }

    public bool IsAdminUsername(string sanitizedUsername) =>
        Enabled && string.Equals(sanitizedUsername, Username, StringComparison.Ordinal);

    public bool CheckPassword(string password) =>
        Enabled && !string.IsNullOrEmpty(password) &&
        CryptographicOperations.FixedTimeEquals(Derive(password, _salt), _hash);

    public void BurnDummyCheck(string password) =>
        CryptographicOperations.FixedTimeEquals(Derive(password ?? string.Empty, _dummySalt), _dummyHash);

    private static byte[] Derive(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, _algorithm, HashSize);
}
