using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class ThreadListParserTests
{
    private readonly ThreadListParser _parser = new();

    [Theory]
    [InlineData("forum-python-desktop.html")]
    [InlineData("forum-python-mobile.html")]
    public void Reads_thread_rows_from_both_layouts(string fixture)
    {
        var listing = _parser.Parse(Fixture.Read(fixture), "python");

        Assert.Equal("python", listing.Slug);
        Assert.Equal("Python", listing.Title);
        Assert.Equal(129, listing.ForumId);
        Assert.True(listing.Threads.Count > 40, $"тем нашлось {listing.Threads.Count}");
        Assert.All(listing.Threads, thread =>
        {
            Assert.True(thread.ThreadId > 0);
            Assert.NotEmpty(thread.Title);
        });
    }

    [Fact]
    public void Reads_counters_and_date_from_desktop_layout()
    {
        var listing = _parser.Parse(Fixture.Read("forum-python-desktop.html"), "python");

        var thread = listing.Threads.Single(t => t.ThreadId == 3050934);

        Assert.Equal("Кривые в OpenGL", thread.Title);
        Assert.Equal("python", thread.ForumSlug);
        Assert.Equal(12, thread.Replies);
        Assert.Equal(4374, thread.Views);
        Assert.NotNull(thread.Preview);
        Assert.Equal(new DateTimeOffset(2026, 4, 29, 16, 47, 0, TimeSpan.FromHours(3)), thread.LastPostAt);
    }

    [Fact]
    public void Detects_sticky_threads_and_page_counts()
    {
        var listing = _parser.Parse(Fixture.Read("forum-python-desktop.html"), "python");

        var sticky = listing.Threads.Single(t => t.ThreadId == 1452827);

        Assert.True(sticky.IsSticky);
        Assert.Equal(4, sticky.PageCount);
        Assert.Equal(74, sticky.Replies);
    }

    [Fact]
    public void Reads_pagination()
    {
        var first = _parser.Parse(Fixture.Read("forum-python-desktop.html"), "python");
        Assert.Equal(1, first.Pagination.CurrentPage);
        Assert.True(first.Pagination.PageCount > 100);
        Assert.True(first.Pagination.HasNext);
        Assert.False(first.Pagination.HasPrevious);

        var second = _parser.Parse(Fixture.Read("forum-python-page2-desktop.html"), "python");
        Assert.Equal(2, second.Pagination.CurrentPage);
        Assert.True(second.Pagination.HasPrevious);
    }

    [Fact]
    public void Mobile_layout_still_yields_reply_counts()
    {
        var listing = _parser.Parse(Fixture.Read("forum-python-mobile.html"), "python");

        var thread = listing.Threads.Single(t => t.ThreadId == 3050934);

        Assert.Equal(12, thread.Replies);
    }
}
