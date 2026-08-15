using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

/// <summary>
/// Блоги. В отличие от тем, форум отдаёт их обычному http-клиенту, так что читаем
/// их не через WebView, а по-честному: разбираем и показываем своей вёрсткой.
/// </summary>
public sealed partial class BlogParser
{
    private readonly HtmlParser _parser = new();

    /// <summary>Список записей блога: /blogs/{userId}/</summary>
    public IReadOnlyList<BlogPost> ParseList(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        using var document = _parser.ParseDocument(html);

        var posts = new List<BlogPost>();
        var block = document.QuerySelector("#blogentries");

        if (block is null)
        {
            return posts;
        }

        foreach (var entry in block.QuerySelectorAll("div[id^='entry']"))
        {
            var link = entry.QuerySelector("a[href*='/blogs/'] span.forumtitle")?.Closest("a");

            if (link is null)
            {
                continue;
            }

            var href = link.GetAttribute("href") ?? string.Empty;
            var (userId, entryId) = Numbers(href);

            if (entryId == 0)
            {
                continue;
            }

            var head = entry.QuerySelector("div.smallfont.shade").CleanText();
            var counters = entry.QuerySelectorAll("div.smallfont").Length > 1
                ? entry.QuerySelectorAll("div.smallfont")[1].CleanText()
                : string.Empty;

            posts.Add(new BlogPost
            {
                EntryId = entryId,
                UserId = userId,
                Title = link.CleanText(),
                Url = href,
                Author = Author(head),
                When = When(head),
                Views = After(counters, "Показов"),
                Comments = After(counters, "Комментарии"),
                Preview = entry.QuerySelector("div.blog_preview").CleanText(),
                Tags = Tags(entry),
            });
        }

        return posts;
    }

    /// <summary>Одна запись: /blogs/{userId}/{entryId}.html</summary>
    public BlogPost? ParseEntry(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        using var document = _parser.ParseDocument(html);

        var title = document.QuerySelector("#blog_title").CleanText();
        var body = document.QuerySelector("#blog_message");

        if (title.Length == 0 || body is null)
        {
            return null;
        }

        var canonical = document.QuerySelector("link[rel='canonical']")?.GetAttribute("href") ?? string.Empty;
        var (userId, entryId) = Numbers(canonical);
        var head = document.QuerySelector("div.smallfont.shade").CleanText();

        return new BlogPost
        {
            EntryId = entryId,
            UserId = userId,
            Title = title,
            Url = canonical,
            Author = Author(head),
            When = When(head),
            BodyHtml = body.InnerHtml,
            Tags = Tags(document.Body),
        };
    }

    private static IReadOnlyList<string> Tags(IElement? root)
    {
        var list = root?.QuerySelector("span[id^='blogtaglist_']");

        if (list is null)
        {
            return [];
        }

        return list.QuerySelectorAll("a")
            .Select(anchor => anchor.CleanText())
            .Where(text => text.Length > 0)
            .ToList();
    }

    // «Запись от tester42 размещена 04.03.2025 в 21:50»
    private static string? Author(string head)
    {
        var match = AuthorPattern().Match(head);

        return match.Success ? match.Groups["name"].Value.Trim() : null;
    }

    private static string? When(string head)
    {
        var match = WhenPattern().Match(head);

        return match.Success ? match.Value : null;
    }

    private static int After(string text, string label)
    {
        var at = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);

        if (at < 0)
        {
            return 0;
        }

        var match = NumberPattern().Match(text[(at + label.Length)..]);

        return match.Success && int.TryParse(match.Value.Replace(" ", string.Empty), out var value) ? value : 0;
    }

    private static (int UserId, int EntryId) Numbers(string href)
    {
        var match = AddressPattern().Match(href);

        if (!match.Success)
        {
            return (0, 0);
        }

        _ = int.TryParse(match.Groups["user"].Value, out var user);
        _ = int.TryParse(match.Groups["entry"].Value, out var entry);

        return (user, entry);
    }

    [GeneratedRegex(@"/blogs/(?<user>\d+)/(?:blog)?(?<entry>\d+)")]
    private static partial Regex AddressPattern();

    [GeneratedRegex(@"Запись от\s+(?<name>[^\s].*?)\s+размещена")]
    private static partial Regex AuthorPattern();

    [GeneratedRegex(@"\d{2}\.\d{2}\.\d{4}(\s+в\s+\d{1,2}:\d{2})?")]
    private static partial Regex WhenPattern();

    [GeneratedRegex(@"[\d\s]+")]
    private static partial Regex NumberPattern();
}
