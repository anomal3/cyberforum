using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

/// <summary>
/// Карта форума (map.php) — единственная страница, где перечислены все разделы сразу.
/// Вёрстка плоская: h3 открывает категорию, а идущие следом абзацы с отступом — её разделы.
/// </summary>
public sealed class ForumMapParser
{
    private readonly HtmlParser _parser = new();

    public IReadOnlyList<ForumNode> Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        using var document = _parser.ParseDocument(html);

        var categories = new List<ForumNode>();
        var current = (Node: (ForumNode?)null, Children: new List<ForumNode>());

        foreach (var element in document.QuerySelectorAll("h3, p"))
        {
            var node = ToNode(element);
            if (node is null)
            {
                continue;
            }

            if (element.TagName.Equals("H3", StringComparison.OrdinalIgnoreCase) ||
                element.QuerySelector("h3") is not null)
            {
                Flush(categories, ref current);
                current = (node, []);
                continue;
            }

            // абзац с отступом — это раздел внутри последней категории
            if (current.Node is not null && (element.GetAttribute("style") ?? string.Empty).Contains("margin-left"))
            {
                current.Children.Add(node);
            }
        }

        Flush(categories, ref current);
        return categories;
    }

    private static void Flush(List<ForumNode> categories, ref (ForumNode? Node, List<ForumNode> Children) current)
    {
        if (current.Node is not null)
        {
            categories.Add(current.Node with { Children = current.Children });
        }

        current = (null, []);
    }

    private static ForumNode? ToNode(IElement element)
    {
        var anchor = element.QuerySelector("a[href]");
        if (anchor is null)
        {
            return null;
        }

        var location = ForumUrls.Parse(anchor.GetAttribute("href") ?? string.Empty);
        if (location.Kind != ForumUrlKind.Forum || string.IsNullOrEmpty(location.Slug))
        {
            return null;
        }

        return new ForumNode
        {
            Slug = location.Slug,
            Title = anchor.CleanText(),
            Description = FindDescription(element),
        };
    }

    // У разделов описание лежит внутри абзаца, а у категорий — снаружи. Карта открывает <p>
    // и суёт внутрь <h3>, поэтому браузерный парсер закрывает абзац раньше времени
    // и описание оказывается соседом заголовка, а не его потомком.
    private static string? FindDescription(IElement element)
    {
        var inside = element.QuerySelector("span.smallfont").CleanText();
        if (inside.Length > 0)
        {
            return inside;
        }

        for (var sibling = element.NextElementSibling; sibling is not null; sibling = sibling.NextElementSibling)
        {
            if (sibling.TagName is "P" or "H3")
            {
                break;
            }

            if (sibling.Matches("span.smallfont"))
            {
                var text = sibling.CleanText();
                return text.Length > 0 ? text : null;
            }
        }

        return null;
    }
}
