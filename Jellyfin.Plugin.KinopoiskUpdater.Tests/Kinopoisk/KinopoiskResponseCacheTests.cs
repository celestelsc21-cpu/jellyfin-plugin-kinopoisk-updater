using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.KinopoiskUpdater.Kinopoisk;
using Xunit;

namespace Jellyfin.Plugin.KinopoiskUpdater.Tests.Kinopoisk;

public class KinopoiskResponseCacheTests
{
    private readonly ManualTimeProvider _time = new();

    [Fact]
    public async Task SameKey_WithinTtl_FetchesOnce()
    {
        var cache = new KinopoiskResponseCache(_time, TimeSpan.FromMinutes(30));
        var calls = 0;

        var first = await cache.GetOrAddAsync("a", () => { calls++; return Task.FromResult<string?>("1"); }, CancellationToken.None);
        var second = await cache.GetOrAddAsync("a", () => { calls++; return Task.FromResult<string?>("2"); }, CancellationToken.None);

        Assert.Equal("1", first);
        Assert.Equal("1", second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ExpiredEntry_IsFetchedAgain()
    {
        var cache = new KinopoiskResponseCache(_time, TimeSpan.FromMinutes(30));
        await cache.GetOrAddAsync("a", () => Task.FromResult<string?>("old"), CancellationToken.None);

        _time.Advance(TimeSpan.FromMinutes(31));
        var value = await cache.GetOrAddAsync("a", () => Task.FromResult<string?>("new"), CancellationToken.None);

        Assert.Equal("new", value);
    }

    [Fact]
    public async Task FailedRequest_IsNotCached()
    {
        var cache = new KinopoiskResponseCache(_time, TimeSpan.FromMinutes(30));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.GetOrAddAsync("a", () => Task.FromException<string?>(new InvalidOperationException()), CancellationToken.None));
        var value = await cache.GetOrAddAsync("a", () => Task.FromResult<string?>("ok"), CancellationToken.None);

        Assert.Equal("ok", value);
    }

    [Fact]
    public async Task SynchronousThrow_IsNotCached()
    {
        var cache = new KinopoiskResponseCache(_time, TimeSpan.FromMinutes(30));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.GetOrAddAsync("a", () => throw new InvalidOperationException(), CancellationToken.None));
        var value = await cache.GetOrAddAsync("a", () => Task.FromResult<string?>("ok"), CancellationToken.None);

        Assert.Equal("ok", value);
    }

    [Fact]
    public async Task NotFound_IsCachedAsNull()
    {
        var cache = new KinopoiskResponseCache(_time, TimeSpan.FromMinutes(30));
        var calls = 0;

        await cache.GetOrAddAsync("a", () => { calls++; return Task.FromResult<string?>(null); }, CancellationToken.None);
        var value = await cache.GetOrAddAsync("a", () => { calls++; return Task.FromResult<string?>("x"); }, CancellationToken.None);

        Assert.Null(value);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task ConcurrentCallers_ShareOneRequest()
    {
        var cache = new KinopoiskResponseCache(_time, TimeSpan.FromMinutes(30));
        var gate = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;

        var first = cache.GetOrAddAsync("a", () => { calls++; return gate.Task; }, CancellationToken.None);
        var second = cache.GetOrAddAsync("a", () => { calls++; return gate.Task; }, CancellationToken.None);
        gate.SetResult("shared");

        Assert.Equal("shared", await first);
        Assert.Equal("shared", await second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task CancelledCaller_DoesNotCancelSharedRequest()
    {
        var cache = new KinopoiskResponseCache(_time, TimeSpan.FromMinutes(30));
        var gate = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource();

        var cancelled = cache.GetOrAddAsync("a", () => gate.Task, cts.Token);
        var other = cache.GetOrAddAsync("a", () => gate.Task, CancellationToken.None);
        await cts.CancelAsync();
        gate.SetResult("done");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        Assert.Equal("done", await other);
    }

    [Fact]
    public async Task ManyKeys_AreTrimmedAfterExpiry()
    {
        var cache = new KinopoiskResponseCache(_time, TimeSpan.FromMinutes(1));
        for (var i = 0; i < KinopoiskResponseCache.SoftLimit; i++)
        {
            await cache.GetOrAddAsync("k" + i, () => Task.FromResult<string?>("v"), CancellationToken.None);
        }

        _time.Advance(TimeSpan.FromMinutes(2));
        await cache.GetOrAddAsync("fresh", () => Task.FromResult<string?>("v"), CancellationToken.None);

        Assert.Equal(1, cache.Count);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
