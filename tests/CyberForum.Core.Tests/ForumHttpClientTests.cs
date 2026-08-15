using System.Net;
using CyberForum.Core.Http;

namespace CyberForum.Core.Tests;

public class ForumHttpClientTests
{
    // Подсовываем клиенту заранее заготовленные ответы вместо настоящей сети
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> reply) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;
            return Task.FromResult(reply(request));
        }
    }

    private static HttpResponseMessage Respond(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body) };

    [Fact]
    public async Task Block_page_becomes_typed_exception()
    {
        var blocked = Fixture.Read("blocked-403.html");
        using var handler = new StubHandler(_ => Respond(HttpStatusCode.Forbidden, blocked));
        using var client = new ForumHttpClient(handler);

        var error = await Assert.ThrowsAsync<ForumBlockedException>(
            () => client.GetStringAsync(ForumUrls.Forum("python")));

        Assert.Equal(ForumUrls.Forum("python"), error.Uri);
    }

    [Fact]
    public async Task Block_page_is_caught_even_with_status_200()
    {
        var blocked = Fixture.Read("blocked-403.html");
        using var handler = new StubHandler(_ => Respond(HttpStatusCode.OK, blocked));
        using var client = new ForumHttpClient(handler);

        await Assert.ThrowsAsync<ForumBlockedException>(
            () => client.GetStringAsync(ForumUrls.Thread("python", 1)));
    }

    [Fact]
    public async Task Normal_page_passes_through()
    {
        var page = Fixture.Read("forum-python-guest.html");
        using var handler = new StubHandler(_ => Respond(HttpStatusCode.OK, page));
        using var client = new ForumHttpClient(handler);

        var body = await client.GetStringAsync(ForumUrls.Forum("python"));

        Assert.Contains("thread_title_", body);
    }

    [Fact]
    public async Task Server_errors_are_retried_then_reported()
    {
        using var handler = new StubHandler(_ => Respond(HttpStatusCode.BadGateway, "oops"));
        using var client = new ForumHttpClient(handler);

        await Assert.ThrowsAsync<ForumUnavailableException>(
            () => client.GetStringAsync(ForumUrls.Home()));

        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task Requests_carry_browser_headers()
    {
        HttpRequestMessage? seen = null;
        using var handler = new StubHandler(request =>
        {
            seen = request;
            return Respond(HttpStatusCode.OK, "<html></html>");
        });
        using var client = new ForumHttpClient(handler);

        await client.GetStringAsync(ForumUrls.Forum("python", 2), referer: ForumUrls.Forum("python"));

        Assert.NotNull(seen);
        Assert.Contains("Chrome", seen!.Headers.UserAgent.ToString());
        Assert.Equal(ForumUrls.Forum("python"), seen.Headers.Referrer);
        Assert.Contains("ru", seen.Headers.AcceptLanguage.ToString());
    }
}
