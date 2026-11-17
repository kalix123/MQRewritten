using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Server.Base.Accounts.Enums;
using Server.Base.Accounts.Extensions;
using Server.Base.Core.Extensions;
using Server.Base.Database.Accounts;
using Server.Base.Logging;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace Web.Razor.Admin;

public enum AdminSignInResult
{
    Success,
    Invalid,
    RateLimited,
    Busy
}

public record AdminSession(string Username, int? AccountId)
{
    public DateTime Created { get; } = DateTime.UtcNow;
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    public bool IsConfigAdmin => AccountId == null;
}

// MQ Rewritten: sign-in and sessions for /admin. The cookie holds a random token; the server keeps only
// its SHA-256, so the cookie can't be forged and signing out (or a restart) kills it for good.
// Every request re-checks the account, so a demoted or banned owner loses access at once.
public class AdminAuth(AccountHandler accountHandler, AdminCredentials credentials, AdminLoginLimiter limiter,
    FileLogger fileLogger, ILogger<AdminAuth> logger)
{
    public const string CookieName = "__Host-mqr-admin";
    public const string ItemKey = "mqr-admin-session";
    public const AccessLevel RequiredLevel = AccessLevel.Owner;

    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan AbsoluteTimeout = TimeSpan.FromHours(8);

    private readonly ConcurrentDictionary<string, AdminSession> _sessions = new();

    public async Task<(AdminSignInResult Result, TimeSpan? RetryAfter)> SignInAsync(HttpContext context, string username, string password)
    {
        var ip = GetClientIp(context);
        username = (username ?? string.Empty).Sanitize();
        password ??= string.Empty;

        if (username.Length > 64 || password.Length > 256)
        {
            Log(ip, username, "Rejected oversized input", LoggerType.Warning);
            limiter.RegisterFailure(ip, username, false);
            return (AdminSignInResult.Invalid, null);
        }

        var isConfigAdmin = credentials.IsAdminUsername(username);

        var locked = limiter.IpLockedFor(ip) ?? (isConfigAdmin ? null : limiter.UsernameLockedFor(username));

        if (locked != null)
        {
            Log(ip, username, "Refused: locked out", LoggerType.Warning);
            return (AdminSignInResult.RateLimited, locked);
        }

        if (!await limiter.HashChecks.WaitAsync(TimeSpan.FromSeconds(10), context.RequestAborted))
        {
            Log(ip, username, "Refused: too many sign-ins at once", LoggerType.Warning);
            return (AdminSignInResult.Busy, null);
        }

        AdminSession session = null;
        string reason;

        try
        {
            if (isConfigAdmin)
            {
                if (credentials.CheckPassword(password))
                    session = new AdminSession(credentials.Username, null);

                reason = session == null ? "Wrong password for the admin login" : "Signed in with the admin login";
            }
            else
            {
                var account = string.IsNullOrEmpty(username) ? null : accountHandler.GetAccountFromUsername(username);

                if (account == null)
                {
                    credentials.BurnDummyCheck(password);
                    reason = "Unknown username";
                }
                else if (!accountHandler.ValidatePassword(account, password))
                    reason = "Wrong password";
                else if (!HasAccess(account))
                    reason = $"Not allowed (access level {account.AccessLevel}{(account.IsBanned() ? ", banned" : string.Empty)})";
                else
                {
                    session = new AdminSession(account.Username, account.Id);
                    reason = "Signed in";
                }
            }
        }
        finally
        {
            limiter.HashChecks.Release();
        }

        if (session == null)
        {
            limiter.RegisterFailure(ip, username, !isConfigAdmin);
            Log(ip, username, reason, LoggerType.Error);
            return (AdminSignInResult.Invalid, null);
        }

        limiter.RegisterSuccess(ip, username);
        Log(ip, username, reason, LoggerType.Debug);

        // Drop any session the browser already had, then issue a fresh token.
        RemoveSession(context);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        _sessions[Hash(token)] = session;
        context.Response.Cookies.Append(CookieName, token, CookieOptions());

        return (AdminSignInResult.Success, null);
    }

    // The signed-in admin for this request, or null. Slides the idle timeout.
    public AdminSession GetSession(HttpContext context)
    {
        if (context.Items.TryGetValue(ItemKey, out var cached))
            return cached as AdminSession;

        AdminSession result = null;

        if (context.Request.Cookies.TryGetValue(CookieName, out var token) && !string.IsNullOrEmpty(token) && token.Length <= 128)
        {
            var key = Hash(token);

            if (_sessions.TryGetValue(key, out var session))
            {
                var now = DateTime.UtcNow;

                if (now - session.LastSeen > IdleTimeout || now - session.Created > AbsoluteTimeout || !StillAllowed(session))
                    _sessions.TryRemove(key, out _);
                else
                {
                    session.LastSeen = now;
                    result = session;
                }
            }
        }

        context.Items[ItemKey] = result;
        PruneExpired();
        return result;
    }

    public void SignOut(HttpContext context)
    {
        var session = GetSession(context);

        RemoveSession(context);
        context.Response.Cookies.Delete(CookieName, CookieOptions());
        context.Items[ItemKey] = null;

        if (session != null)
            Log(GetClientIp(context), session.Username, "Signed out", LoggerType.Debug);
    }

    // Behind Caddy (or docker's NAT) the socket address is the proxy, so trust X-Forwarded-For only
    // from loopback/private peers. A direct hit from the internet can't fake its address this way.
    public static string GetClientIp(HttpContext context)
    {
        var remote = context.Connection.RemoteIpAddress;

        if (remote == null)
            return "unknown";

        if (remote.IsIPv4MappedToIPv6)
            remote = remote.MapToIPv4();

        if (IsPrivate(remote))
        {
            var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
            var last = forwarded.Split(',').LastOrDefault()?.Trim();

            if (!string.IsNullOrEmpty(last) && IPAddress.TryParse(last, out var parsed))
                return parsed.ToString();
        }

        return remote.ToString();
    }

    private bool StillAllowed(AdminSession session)
    {
        if (session.IsConfigAdmin)
            return credentials.IsAdminUsername(session.Username);

        var account = accountHandler.GetAccountFromId(session.AccountId!.Value);
        return account != null && HasAccess(account);
    }

    private static bool HasAccess(AccountModel account) =>
        account.AccessLevel >= RequiredLevel && !account.IsBanned();

    private void RemoveSession(HttpContext context)
    {
        if (context.Request.Cookies.TryGetValue(CookieName, out var token) && !string.IsNullOrEmpty(token))
            _sessions.TryRemove(Hash(token), out _);
    }

    private void PruneExpired()
    {
        if (_sessions.Count < 100)
            return;

        var now = DateTime.UtcNow;

        foreach (var pair in _sessions)
            if (now - pair.Value.LastSeen > IdleTimeout || now - pair.Value.Created > AbsoluteTimeout)
                _sessions.TryRemove(pair.Key, out _);
    }

    private static CookieOptions CookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true
    };

    private static bool IsPrivate(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip))
            return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = ip.GetAddressBytes();
            return b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168);
        }

        return ip.AddressFamily == AddressFamily.InterNetworkV6 && (ip.GetAddressBytes()[0] & 0xFE) == 0xFC;
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private void Log(string ip, string username, string result, LoggerType type)
    {
        var safeName = new string((username ?? string.Empty).Where(c => !char.IsControl(c)).Take(64).ToArray());

        fileLogger.WriteGenericLog<AdminAuth>("admin-login", $"Admin sign-in from {ip}", $"{result} for '{safeName}'", type);

        if (type is LoggerType.Error or LoggerType.Warning)
            logger.LogWarning("Admin sign-in from {Ip} for '{Username}': {Result}", ip, safeName, result);
        else
            logger.LogInformation("Admin sign-in from {Ip} for '{Username}': {Result}", ip, safeName, result);
    }
}
