using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class RssFeedParserTests
{
    private readonly RssFeedParser _parser = new();

    [Fact]
    public void Reads_forum_wide_feed()
    {
        var items = _parser.Parse(Fixture.Read("rss-all.xml"));

        Assert.NotEmpty(items);
        Assert.All(items, item =>
        {
            Assert.True(item.ThreadId > 0);
            Assert.NotEmpty(item.Title);
            Assert.NotEmpty(item.Link);
        });
    }

    [Fact]
    public void Keeps_full_post_html_and_author()
    {
        var items = _parser.Parse(Fixture.Read("rss-all.xml"));
        var withContent = items.First(i => !string.IsNullOrWhiteSpace(i.ContentHtml));

        Assert.False(string.IsNullOrWhiteSpace(withContent.Author));
        Assert.NotNull(withContent.PublishedAt);
        Assert.False(string.IsNullOrWhiteSpace(withContent.ForumSlug));
        Assert.False(string.IsNullOrWhiteSpace(withContent.ForumTitle));
    }

    [Fact]
    public void Section_feed_stays_within_its_section()
    {
        var items = _parser.Parse(Fixture.Read("rss-python.xml"));

        Assert.NotEmpty(items);
        Assert.All(items, item => Assert.NotNull(item.ForumSlug));
    }

    [Fact]
    public void Broken_xml_yields_empty_list_instead_of_throwing()
    {
        Assert.Empty(_parser.Parse("<rss><channel><item>"));
    }
}
