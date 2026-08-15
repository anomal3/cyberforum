using CyberForum.Core.Models;
using CyberForum.Core.Parsing;

namespace CyberForum.Core.Tests;

public class ForumHomeParserTests
{
    private static IReadOnlyList<ForumNode> Tree() =>
        new ForumHomeParser().Parse(Fixture.Read("home.html"));

    [Fact]
    public void Категории_разбираются()
    {
        var tree = Tree();

        Assert.NotEmpty(tree);
        Assert.Contains(tree, node => node.Title.Contains("Форум программистов", StringComparison.Ordinal));
    }

    [Fact]
    public void Разделы_лежат_внутри_категорий()
    {
        var programming = Tree().First(node => node.Slug == "programming");

        Assert.Contains(programming.Children, node => node.Slug == "c-cpp");
        Assert.Contains(programming.Children, node => node.Slug == "dot-net");
    }

    // Ради этого всё и затевалось: C# и его окрестности должны сидеть внутри .NET,
    // а не лежать вперемешку с остальными разделами, как в карте форума.
    [Fact]
    public void Шарп_и_его_родня_внутри_дотнета()
    {
        var dotNet = Tree()
            .SelectMany(category => category.Children)
            .First(section => section.Slug == "dot-net");

        var slugs = dotNet.Children.Select(child => child.Slug).ToList();

        Assert.Contains("csharp-net", slugs);
        Assert.Contains("csharp-beginners", slugs);
        Assert.Contains("asp-net", slugs);
        Assert.Contains("windows-forms", slugs);
        Assert.DoesNotContain("dot-net", slugs);
    }

    [Fact]
    public void Разделов_набирается_столько_же_сколько_на_странице()
    {
        var tree = Tree();
        var sections = tree.SelectMany(category => category.Children).ToList();
        var all = sections.Concat(sections.SelectMany(section => section.Children)).ToList();

        Assert.True(sections.Count > 50, $"разделов верхнего уровня всего {sections.Count}");
        Assert.True(all.Count > 400, $"разделов вместе с подразделами всего {all.Count}");
    }
}
