using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

/// <summary>
/// Дерево разделов с главной страницы. Карта форума (map.php) для этого не годится:
/// она плоская, и «C# для начинающих», «C#: Web, ASP.NET» и «C# Windows Forms» лежат
/// в ней вперемешку с остальными, хотя на самом деле все они внутри раздела .NET.
/// На главной вложенность видна: категория — таблица, раздел — её ячейка, а подразделы
/// перечислены внутри ячейки мелким шрифтом.
/// </summary>
public sealed class ForumHomeParser
{
    private readonly HtmlParser _parser = new();

    public IReadOnlyList<ForumNode> Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        using var document = _parser.ParseDocument(html);

        var categories = new List<ForumNode>();

        foreach (var table in document.QuerySelectorAll("table[id^='kr_table_']"))
        {
            var category = ToNode(table.QuerySelector("td.tcat a[href]"));

            if (category is null)
            {
                continue;
            }

            var sections = new List<ForumNode>();

            foreach (var cell in table.QuerySelectorAll("td[id^='f']"))
            {
                var head = cell.QuerySelector("span.forumtitle")?.Closest("a");
                var section = ToNode(head);

                if (section is null)
                {
                    continue;
                }

                var children = new List<ForumNode>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { section.Slug };

                // подразделы живут во вложенной таблице той же ячейки
                foreach (var link in cell.QuerySelectorAll("table a[href]"))
                {
                    var child = ToNode(link);

                    if (child is not null && seen.Add(child.Slug))
                    {
                        children.Add(child);
                    }
                }

                sections.Add(section with { Children = children });
            }

            if (sections.Count > 0)
            {
                categories.Add(category with { Children = sections });
            }
        }

        return categories;
    }

    private static ForumNode? ToNode(IElement? anchor)
    {
        if (anchor is null)
        {
            return null;
        }

        var location = ForumUrls.Parse(anchor.GetAttribute("href") ?? string.Empty);

        if (location.Kind != ForumUrlKind.Forum || string.IsNullOrEmpty(location.Slug))
        {
            return null;
        }

        var title = anchor.CleanText();

        return title.Length == 0
            ? null
            : new ForumNode
            {
                Slug = location.Slug,
                Title = title,
            };
    }
}
