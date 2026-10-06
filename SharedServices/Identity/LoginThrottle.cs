using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace SharedServices.Identity
{
    /// <summary>Holds back an address after too many failed sign-ins, counting only failures as an office shares one IP.</summary>
    public sealed class LoginThrottle
    {
        public const int MaxFailures = 20;
        public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

        private readonly ConcurrentDictionary<string, (DateTime Start, int Count)> _failures = new();
        private DateTime _nextPrune = DateTime.UtcNow;

        /// <summary>What sign-in limits count an address under, which for IPv6 is its /64 because one line holds that whole range.</summary>
        public static string? KeyFor(IPAddress? address)
        {
            if (address == null) return null;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            if (address.AddressFamily != AddressFamily.InterNetworkV6) return address.ToString();

            var bytes = address.GetAddressBytes();
            Array.Clear(bytes, 8, 8);
            return $"{new IPAddress(bytes)}/64";
        }

        /// <summary>How long the address must wait, or null when it may try.</summary>
        public TimeSpan? RetryAfter(IPAddress? address)
        {
            if (KeyFor(address) is not { } key || !_failures.TryGetValue(key, out var entry)) return null;
            var left = entry.Start + Window - DateTime.UtcNow;
            return entry.Count >= MaxFailures && left > TimeSpan.Zero ? left : null;
        }

        public void RecordFailure(IPAddress? address)
        {
            if (KeyFor(address) is not { } key) return;
            var now = DateTime.UtcNow;
            _failures.AddOrUpdate(key, (now, 1),
                (_, e) => e.Start + Window <= now ? (now, 1) : (e.Start, e.Count + 1));

            if (now >= _nextPrune)
            {
                _nextPrune = now + Window;
                foreach (var (stale, e) in _failures)
                    if (e.Start + Window <= now) _failures.TryRemove(stale, out _);
            }
        }
    }
}
