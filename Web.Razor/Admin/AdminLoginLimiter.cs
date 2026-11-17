using System.Collections.Concurrent;

namespace Web.Razor.Admin;

// MQ Rewritten: brute-force protection for the admin sign-in. Failures are counted per IP and per
// username in a sliding window; going over the limit locks that IP or username out for a while.
// Everything is in memory, so a restart clears it.
public class AdminLoginLimiter
{
    public const int MaxFailuresPerIp = 5;
    public const int MaxFailuresPerUsername = 10;
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan Lockout = TimeSpan.FromMinutes(15);

    // Stops a flood of sign-ins from tying up the CPU with password hashing.
    public readonly SemaphoreSlim HashChecks = new(2, 2);

    private readonly ConcurrentDictionary<string, Tracker> _ips = new();
    private readonly ConcurrentDictionary<string, Tracker> _usernames = new();

    public TimeSpan? IpLockedFor(string ip) => LockedFor(_ips, ip);
    public TimeSpan? UsernameLockedFor(string username) => LockedFor(_usernames, username);

    public void RegisterFailure(string ip, string username, bool countUsername)
    {
        Fail(_ips, ip, MaxFailuresPerIp);

        if (countUsername && !string.IsNullOrEmpty(username))
            Fail(_usernames, username, MaxFailuresPerUsername);

        Prune();
    }

    public void RegisterSuccess(string ip, string username)
    {
        _ips.TryRemove(ip, out _);

        if (!string.IsNullOrEmpty(username))
            _usernames.TryRemove(username, out _);
    }

    private static TimeSpan? LockedFor(ConcurrentDictionary<string, Tracker> map, string key)
    {
        if (string.IsNullOrEmpty(key) || !map.TryGetValue(key, out var tracker))
            return null;

        lock (tracker)
        {
            var left = tracker.LockedUntil - DateTime.UtcNow;
            return left > TimeSpan.Zero ? left : null;
        }
    }

    private static void Fail(ConcurrentDictionary<string, Tracker> map, string key, int max)
    {
        var tracker = map.GetOrAdd(key, _ => new Tracker());
        var now = DateTime.UtcNow;

        lock (tracker)
        {
            tracker.Failures.RemoveAll(t => now - t > Window);
            tracker.Failures.Add(now);

            if (tracker.Failures.Count >= max)
            {
                tracker.LockedUntil = now + Lockout;
                tracker.Failures.Clear();
            }
        }
    }

    private void Prune()
    {
        if (_ips.Count + _usernames.Count < 1000)
            return;

        var now = DateTime.UtcNow;

        foreach (var map in new[] { _ips, _usernames })
            foreach (var pair in map)
                lock (pair.Value)
                    if (pair.Value.LockedUntil < now && pair.Value.Failures.TrueForAll(t => now - t > Window))
                        map.TryRemove(pair.Key, out _);
    }

    private class Tracker
    {
        public readonly List<DateTime> Failures = [];
        public DateTime LockedUntil = DateTime.MinValue;
    }
}
