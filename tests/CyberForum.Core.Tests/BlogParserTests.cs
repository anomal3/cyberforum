using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class BlogParserTests
{
    [Fact]
    public void Список_записей_разбирается()
    {
        var posts = new BlogParser().ParseList(Fixture.Read("blog-list.html"));

        Assert.NotEmpty(posts);

        var first = posts[0];

        Assert.Contains("Winforstrap", first.Title);
        Assert.Equal(9921, first.EntryId);
        Assert.Equal(100500, first.UserId);
        Assert.Equal("tester42", first.Author);
        Assert.Equal("04.03.2025 в 21:50", first.When);
        Assert.True(first.Views > 1000, $"просмотров насчитали {first.Views}");
        Assert.NotNull(first.Preview);
        Assert.Contains("c#", first.Tags);
    }

    [Fact]
    public void Запись_читается_целиком()
    {
        var post = new BlogParser().ParseEntry(Fixture.Read("blog-entry.html"));

        Assert.NotNull(post);
        Assert.Contains("Winforstrap", post!.Title);
        Assert.Equal(9921, post.EntryId);
        Assert.Equal(100500, post.UserId);
        Assert.NotNull(post.BodyHtml);
        Assert.Contains("MAUI", post.BodyHtml!);
        Assert.Contains("winforms", post.Tags);
    }

    [Fact]
    public void Комментарии_под_записью_читаются()
    {
        var post = new BlogParser().ParseEntry(Fixture.Read("blog-entry-comments.html"));

        Assert.NotNull(post);
        Assert.Equal(4, post.CommentList.Count);

        var first = post.CommentList[0];

        Assert.Equal(27071, first.CommentId);
        Assert.Equal("komment1", first.Author);
        Assert.Equal(100600, first.AuthorId);
        Assert.Equal("28.01.2021 в 20:47", first.When);
        Assert.Contains("Гитхаб", first.BodyHtml);
        Assert.NotNull(first.AvatarUrl);
    }

    [Fact]
    public void У_записи_без_комментариев_список_пустой()
    {
        var post = new BlogParser().ParseEntry(Fixture.Read("blog-entry.html"));

        Assert.NotNull(post);
        Assert.Empty(post.CommentList);
    }
}
