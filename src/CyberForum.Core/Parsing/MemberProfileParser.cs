using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

/// <summary>
/// Страница участника: имя, когда был, мини-статистика, «обо мне» и блог.
/// Всё это лежит блоками с говорящими id, так что берём их поимённо.
/// </summary>
public sealed partial class MemberProfileParser
{
    private readonly HtmlParser _parser = new();

    public MemberProfile Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        using var document = _parser.ParseDocument(html);

        var name = document.QuerySelector("#username_box h1").CleanText();

        if (name.Length == 0)
        {
            name = (document.Title ?? string.Empty).Split(" - ")[0].Trim();
        }

        return new MemberProfile
        {
            UserId = FindUserId(document),
            UserName = name,
            AvatarUrl = Avatar(document),
            LastActivity = Activity(document),
            Stats = Fields(document.QuerySelector("#collapseobj_stats_mini")),
            About = Fields(document.QuerySelector("#collapseobj_aboutme")),
            Blog = Blog(document),
        };
    }

    // мини-статистика и «обо мне» свёрстаны одинаково: dt — название, dd — значение
    private static List<ProfileField> Fields(IElement? block)
    {
        var fields = new List<ProfileField>();

        if (block is null)
        {
            return fields;
        }

        // рядом со значением форум держит скрипт-редактор, и его текст иначе
        // приклеивается к самому значению
        foreach (var script in block.QuerySelectorAll("script, style"))
        {
            script.Remove();
        }

        foreach (var list in block.QuerySelectorAll("dl"))
        {
            var names = list.QuerySelectorAll("dt");
            var values = list.QuerySelectorAll("dd");

            for (var i = 0; i < names.Length && i < values.Length; i++)
            {
                var name = names[i].CleanText();
                var value = values[i].CleanText();

                if (name.Length == 0 || value.Length == 0 ||
                    value.Equals("Недоступно", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                fields.Add(new ProfileField(name, value));
            }
        }

        return fields;
    }

    private static List<BlogEntry> Blog(IDocument document)
    {
        var entries = new List<BlogEntry>();
        var block = document.QuerySelector("#collapseobj_blog");

        if (block is null)
        {
            return entries;
        }

        foreach (var anchor in block.QuerySelectorAll("a[href*='/blogs/']"))
        {
            var href = anchor.GetAttribute("href") ?? string.Empty;
            var title = anchor.CleanText();

            // «Просмотреть блог», «Комментарии» и «Читать дальше» ведут туда же,
            // но записями не являются — в списке они лишние
            if (title.Length == 0 ||
                !EntryPattern().IsMatch(href) ||
                href.Contains('#') ||
                title.StartsWith("Просмотреть блог", StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("Комментарии", StringComparison.OrdinalIgnoreCase) ||
                title.StartsWith("Читать дальше", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var when = anchor.Closest("div")?.ParentElement.CleanText();
            var stamp = when is null ? null : WhenPattern().Match(when).Value;

            entries.Add(new BlogEntry(title, href, string.IsNullOrEmpty(stamp) ? null : stamp));
        }

        return entries;
    }

    private static string? Avatar(IDocument document)
    {
        var image = document.QuerySelector("img[src*='customavatars']");

        return image?.GetAttribute("src");
    }

    private static string? Activity(IDocument document)
    {
        var text = document.QuerySelector("#last_online").CleanText();

        return text.Length == 0 ? null : text.Replace("Последняя активность:", string.Empty).Trim();
    }

    private static int FindUserId(IDocument document)
    {
        foreach (var anchor in document.QuerySelectorAll("a[href*='/members/'], a[href*='u=']"))
        {
            var match = UserIdPattern().Match(anchor.GetAttribute("href") ?? string.Empty);

            if (match.Success && int.TryParse(match.Groups["id"].Value, out var id))
            {
                return id;
            }
        }

        return 0;
    }

    [GeneratedRegex(@"/blogs/\d+/\d+")]
    private static partial Regex EntryPattern();

    [GeneratedRegex(@"\d{2}\.\d{2}\.\d{4}(\s+в\s+\d{1,2}:\d{2})?")]
    private static partial Regex WhenPattern();

    [GeneratedRegex(@"(?:members/|[?&]u=)(?<id>\d+)")]
    private static partial Regex UserIdPattern();
}
