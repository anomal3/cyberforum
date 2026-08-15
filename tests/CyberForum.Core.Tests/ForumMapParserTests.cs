using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class ForumMapParserTests
{
    private readonly ForumMapParser _parser = new();

    [Fact]
    public void Builds_category_tree_from_site_map()
    {
        var categories = _parser.Parse(Fixture.Read("map.html"));

        Assert.True(categories.Count >= 5, $"категорий нашлось {categories.Count}");
        Assert.All(categories, category =>
        {
            Assert.NotEmpty(category.Slug);
            Assert.NotEmpty(category.Title);
        });

        var programming = categories.Single(c => c.Slug == "programming");
        Assert.Equal("Форум программистов", programming.Title);
        Assert.NotNull(programming.Description);
        Assert.Contains(programming.Children, c => c.Slug == "cpp-beginners");
        Assert.Contains(programming.Children, c => c.Slug == "csharp-beginners");
    }

    [Fact]
    public void Covers_the_whole_forum()
    {
        var categories = _parser.Parse(Fixture.Read("map.html"));
        var total = categories.Sum(c => c.Children.Count) + categories.Count;

        Assert.True(total > 400, $"разделов нашлось {total}, ожидалось больше 400");
    }

    [Fact]
    public void Sections_carry_descriptions()
    {
        var categories = _parser.Parse(Fixture.Read("map.html"));

        var python = categories
            .SelectMany(c => c.Children)
            .First(c => c.Slug == "python");

        Assert.Equal("Python", python.Title);
        Assert.False(string.IsNullOrWhiteSpace(python.Description));
    }
}
