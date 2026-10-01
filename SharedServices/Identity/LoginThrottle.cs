using System.Collections.Concurrent;

namespace SharedServices.Identity
{
    /// <summary>
    /// Failed sign-ins per client address (UI and /api/auth/login). Only failures count: everyone
    /// in an office reaches the public site from one address, and counting every sign-in (as the
    /// rate limiter did) refused the sixth colleague within ten minutes. After
    /// <see cref="MaxFailures"/> failures in <see cref="Window"/> that address waits until the window ends;
    /// per-account lockout (identity options) still applies on top.
    /// </summary>
    public sealed class LoginThrottle
    {
        public const int MaxFailures = 20;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        private readonly ConcurrentDictionary<string, (DateTime Start, int Count)> _failures = new();
        private DateTime _nextPrune = DateTime.UtcNow;

        /// <summary>How long the address must wait, or null when it may try.</summary>
        public TimeSpan? RetryAfter(string? address)
        {
            if (address == null || !_failures.TryGetValue(address, out var entry)) return null;
            var left = entry.Start + Window - DateTime.UtcNow;
            return entry.Count >= MaxFailures && left > TimeSpan.Zero ? left : null;
        }

        public void RecordFailure(string? address)
        {
            if (address == null) return;
            var now = DateTime.UtcNow;
            _failures.AddOrUpdate(address, (now, 1),
                (_, e) => e.Start + Window <= now ? (now, 1) : (e.Start, e.Count + 1));

            if (now >= _nextPrune)
            {
                _nextPrune = now + Window;
                foreach (var (key, e) in _failures)
                    if (e.Start + Window <= now) _failures.TryRemove(key, out _);
            }
        }
    }
}
