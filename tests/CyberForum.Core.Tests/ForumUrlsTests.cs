namespace CyberForum.Core.Tests;

public class ForumUrlsTests
{
    [Theory]
    [InlineData("python", 1, "https://www.cyberforum.ru/python/")]
    [InlineData("python", 2, "https://www.cyberforum.ru/python-page2.html")]
    [InlineData("csharp-beginners", 166, "https://www.cyberforum.ru/csharp-beginners-page166.html")]
    public void Forum_builds_vbseo_urls(string slug, int page, string expected) =>
        Assert.Equal(expected, ForumUrls.Forum(slug, page).ToString());

    [Theory]
    [InlineData("python", 3225030, 1, "https://www.cyberforum.ru/python/thread3225030.html")]
    [InlineData("python", 3225030, 4, "https://www.cyberforum.ru/python/thread3225030-page4.html")]
    public void Thread_builds_vbseo_urls(string slug, int id, int page, string expected) =>
        Assert.Equal(expected, ForumUrls.Thread(slug, id, page).ToString());

    [Fact]
    public void Rss_targets_whole_forum_or_single_section()
    {
        Assert.Equal("https://www.cyberforum.ru/external.php?type=RSS2", ForumUrls.Rss().ToString());
        Assert.Equal("https://www.cyberforum.ru/external.php?type=RSS2&forumids=129", ForumUrls.Rss(129).ToString());
    }

    [Fact]
    public void Parse_reads_thread_url()
    {
        var location = ForumUrls.Parse("https://www.cyberforum.ru/python-tasks/thread3225030-page2.html");

        Assert.Equal(ForumUrlKind.Thread, location.Kind);
        Assert.Equal("python-tasks", location.Slug);
        Assert.Equal(3225030, location.ThreadId);
        Assert.Equal(2, location.Page);
    }

    [Fact]
    public void Parse_reads_forum_url()
    {
        var first = ForumUrls.Parse("https://www.cyberforum.ru/cpp-beginners/");
        Assert.Equal(ForumUrlKind.Forum, first.Kind);
        Assert.Equal("cpp-beginners", first.Slug);
        Assert.Equal(1, first.Page);

        var second = ForumUrls.Parse("https://www.cyberforum.ru/cpp-beginners-page7.html");
        Assert.Equal(ForumUrlKind.Forum, second.Kind);
        Assert.Equal("cpp-beginners", second.Slug);
        Assert.Equal(7, second.Page);
    }

    [Fact]
    public void Parse_ignores_foreign_hosts()
    {
        Assert.Equal(ForumUrlKind.Unknown, ForumUrls.Parse("https://example.com/python/thread1.html").Kind);
    }

    [Fact]
    public void Absolute_expands_protocol_relative_cdn_links()
    {
        Assert.Equal(
            "https://cyberstatic.net/images/misc/tag.png",
            ForumUrls.Absolute("//cyberstatic.net/images/misc/tag.png")!.ToString());
    }

    [Fact]
    public void ThreadIdFromUrl_reads_id_or_returns_null()
    {
        Assert.Equal(3050934, ForumUrls.ThreadIdFromUrl("https://www.cyberforum.ru/python/thread3050934.html"));
        Assert.Null(ForumUrls.ThreadIdFromUrl("https://www.cyberforum.ru/python/"));
    }
}
