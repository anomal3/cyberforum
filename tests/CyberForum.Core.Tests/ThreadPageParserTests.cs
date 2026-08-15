using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class ThreadPageParserTests
{
    private readonly ThreadPageParser _parser = new();

    [Fact]
    public void Reads_thread_header()
    {
        var thread = _parser.Parse(Fixture.Read("thread-nosql-auth.html"));

        Assert.Equal(3212200, thread.ThreadId);
        Assert.Equal("python", thread.ForumSlug);
        Assert.StartsWith("А я вот тут базу данных сварганил", thread.Title);
        Assert.Equal(1, thread.Pagination.CurrentPage);
    }

    [Fact]
    public void Reads_every_post_on_the_page()
    {
        var thread = _parser.Parse(Fixture.Read("thread-nosql-auth.html"));

        Assert.Equal(20, thread.Posts.Count);
        Assert.All(thread.Posts, post =>
        {
            Assert.True(post.PostId > 0);
            Assert.NotEmpty(post.Author);
            Assert.NotEmpty(post.ContentHtml);
        });
    }

    [Fact]
    public void Reads_author_date_and_number_of_first_post()
    {
        var thread = _parser.Parse(Fixture.Read("thread-nosql-auth.html"));

        var first = thread.Posts[0];

        Assert.Equal(17627670, first.PostId);
        Assert.Equal("Zloyalex100", first.Author);
        Assert.Equal(1841832, first.AuthorId);
        Assert.Equal(1, first.Number);
        Assert.Equal(new DateTimeOffset(2025, 8, 16, 16, 56, 0, TimeSpan.FromHours(3)), first.PostedAt);
    }

    [Fact]
    public void Second_post_keeps_its_own_date_and_number()
    {
        var thread = _parser.Parse(Fixture.Read("thread-nosql-auth.html"));

        var second = thread.Posts[1];

        Assert.Equal(17627679, second.PostId);
        Assert.Equal(2, second.Number);
        Assert.Equal(new DateTimeOffset(2025, 8, 16, 17, 22, 0, TimeSpan.FromHours(3)), second.PostedAt);
    }

    [Fact]
    public void Post_bodies_keep_code_blocks()
    {
        var thread = _parser.Parse(Fixture.Read("thread-nosql-auth.html"));

        Assert.Contains(thread.Posts, post => post.ContentHtml.Contains("codeblock", StringComparison.Ordinal));
    }

    [Fact]
    public void Posts_from_different_authors_are_distinguished()
    {
        var thread = _parser.Parse(Fixture.Read("thread-nosql-auth.html"));

        var authors = thread.Posts.Select(p => p.Author).Distinct().ToList();

        Assert.True(authors.Count >= 3, $"авторов нашлось {authors.Count}");
        Assert.Contains("ViachaslauK", authors);
    }
}
