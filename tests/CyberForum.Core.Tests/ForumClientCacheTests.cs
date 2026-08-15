using System.Net;
using CyberForum.Core.Http;
using CyberForum.Core.Storage;

namespace CyberForum.Core.Tests;

public class ForumClientCacheTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cf-client-{Guid.NewGuid():N}");
    private CacheStore _cache = null!;

    private sealed class StubHandler(Func<int, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(reply(Calls));
        }
    }

    public Task InitializeAsync()
    {
        _cache = new CacheStore(_path) { FreshFor = TimeSpan.FromMinutes(10) };

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _cache.DisposeAsync();
        Directory.Delete(_path, recursive: true);
    }

    [Fact]
    public async Task Second_call_is_served_from_cache()
    {
        var page = Fixture.Read("forum-python-guest.html");
        using var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(page),
        });

        using var http = new ForumHttpClient(handler);
        var client = new ForumClient(http, _cache);

        var first = await client.GetListingAsync("python");
        var second = await client.GetListingAsync("python");

        Assert.Equal(first.Threads.Count, second.Threads.Count);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Network_failure_falls_back_to_what_we_already_saw()
    {
        var page = Fixture.Read("forum-python-guest.html");

        // первый заход удачный, дальше форум как будто отвалился
        using var handler = new StubHandler(call => call == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(page) }
            : throw new HttpRequestException("сеть кончилась"));

        using var http = new ForumHttpClient(handler);
        var client = new ForumClient(http, _cache);

        await client.GetListingAsync("python");

        // протухаем кэш принудительно, чтобы клиент полез в сеть
        await _cache.ClearOlderThanAsync(TimeSpan.Zero);
        await _cache.SavePageAsync(ForumUrls.Forum("python"), page);
        var stale = new CacheStore(_path) { FreshFor = TimeSpan.Zero };
        var offline = new ForumClient(http, stale);

        var listing = await offline.GetListingAsync("python");

        Assert.NotEmpty(listing.Threads);
        await stale.DisposeAsync();
    }

    [Fact]
    public async Task Without_cache_the_error_reaches_the_caller()
    {
        using var handler = new StubHandler(_ => throw new HttpRequestException("сеть кончилась"));
        using var http = new ForumHttpClient(handler);
        var client = new ForumClient(http);

        await Assert.ThrowsAsync<ForumUnavailableException>(() => client.GetListingAsync("python"));
    }
}
