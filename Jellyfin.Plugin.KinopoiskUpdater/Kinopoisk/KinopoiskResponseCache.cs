using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;

/// <summary>
/// Short-lived in-memory cache of raw API responses keyed by request path.
/// </summary>
/// <remarks>
/// Refreshing a series asks for the same card and episode list once per season and
/// episode; the cache turns that into a single request. Concurrent callers of the same
/// key share one in-flight request. Failed requests are not cached.
/// </remarks>
internal sealed class KinopoiskResponseCache
{
    /// <summary>
    /// Entry count above which expired entries are purged.
    /// </summary>
    internal const int SoftLimit = 512;

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _timeToLive;

    /// <summary>
    /// Initializes a new instance of the <see cref="KinopoiskResponseCache"/> class.
    /// </summary>
    /// <param name="timeProvider">Clock.</param>
    /// <param name="timeToLive">How long a successful response is reused.</param>
    public KinopoiskResponseCache(TimeProvider timeProvider, TimeSpan timeToLive)
    {
        _timeProvider = timeProvider;
        _timeToLive = timeToLive;
    }

    /// <summary>
    /// Gets the number of stored entries (for tests and diagnostics).
    /// </summary>
    public int Count => _entries.Count;

    /// <summary>
    /// Returns the cached response for <paramref name="key"/> or starts <paramref name="factory"/>.
    /// </summary>
    /// <param name="key">Request path.</param>
    /// <param name="factory">Performs the request; must not depend on the caller's token.</param>
    /// <param name="cancellationToken">Cancels waiting for this caller only.</param>
    /// <returns>Response body, or <c>null</c> for "not found".</returns>
    public Task<string?> GetOrAddAsync(string key, Func<Task<string?>> factory, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var entry = _entries.AddOrUpdate(
            key,
            _ => new Entry(factory, now + _timeToLive),
            (_, existing) => existing.IsUsable(now) ? existing : new Entry(factory, now + _timeToLive));

        var task = entry.Value;
        if (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                _ => _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry)),
                CancellationToken.None,
                TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
        else if (!task.IsCompletedSuccessfully)
        {
            _entries.TryRemove(new KeyValuePair<string, Entry>(key, entry));
        }

        Trim(now);
        return task.WaitAsync(cancellationToken);
    }

    private void Trim(DateTimeOffset now)
    {
        if (_entries.Count <= SoftLimit)
        {
            return;
        }

        foreach (var pair in _entries)
        {
            if (!pair.Value.IsUsable(now))
            {
                _entries.TryRemove(pair);
            }
        }

        // Still too many live entries (unusual): start over rather than grow without bound.
        if (_entries.Count > SoftLimit * 2)
        {
            _entries.Clear();
        }
    }

    private sealed class Entry
    {
        private readonly Lazy<Task<string?>> _value;

        public Entry(Func<Task<string?>> factory, DateTimeOffset expiresAt)
        {
            _value = new Lazy<Task<string?>>(() => Start(factory), LazyThreadSafetyMode.ExecutionAndPublication);
            ExpiresAt = expiresAt;
        }

        public DateTimeOffset ExpiresAt { get; }

        public Task<string?> Value => _value.Value;

        public bool IsUsable(DateTimeOffset now)
        {
            if (ExpiresAt <= now)
            {
                return false;
            }

            // Not started yet, still running or succeeded: reuse. Faulted or cancelled: replace.
            return !_value.IsValueCreated || !_value.Value.IsCompleted || _value.Value.IsCompletedSuccessfully;
        }

        // A synchronous throw would be cached by Lazy forever; turn it into a faulted task instead.
        private static Task<string?> Start(Func<Task<string?>> factory)
        {
            try
            {
                return factory();
            }
            catch (Exception ex)
            {
                return Task.FromException<string?>(ex);
            }
        }
    }
}
