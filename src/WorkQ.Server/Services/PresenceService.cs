using System.Collections.Concurrent;

namespace WorkQ.Server.Services;

public sealed class PresenceService
{
    private readonly ConcurrentDictionary<long, int> _connectionCounts = new();

    public bool IsOnline(long userId) => _connectionCounts.ContainsKey(userId);

    public bool MarkOnline(long userId) =>
        _connectionCounts.AddOrUpdate(userId, 1, (_, count) => count + 1) == 1;

    public bool MarkOffline(long userId)
    {
        var wasLast = false;
        _connectionCounts.AddOrUpdate(
            userId,
            0,
            (_, count) =>
            {
                var next = Math.Max(0, count - 1);
                wasLast = next == 0;
                return next;
            });

        if (_connectionCounts.TryGetValue(userId, out var count) && count == 0)
        {
            _connectionCounts.TryRemove(userId, out _);
        }

        return wasLast;
    }
}
