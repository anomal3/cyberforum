using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

/// <summary>
/// Разбирает страницу раздела в список тем. Форум отдаёт разную вёрстку мобильным
/// и десктопным клиентам — в мобильной нет колонок со счётчиками, так что читаем оба варианта.
/// </summary>
public sealed partial class ThreadListParser
{
    private readonly HtmlParser _parser = new();

    public ForumListing Parse(string html, string slug)
    {
        ArgumentNullException.ThrowIfNull(html);

        using var document = _parser.ParseDocument(html);

        return new ForumListing
        {
            Slug = slug.Trim('/'),
            Title = ParsingHelpers.CleanText(document.QuerySelector("h1")),
            ForumId = ExtractForumId(document),
            Threads = document
                .QuerySelectorAll("tr[id^='vbpostrow_']")
                .Select(ParseRow)
                .OfType<ThreadSummary>()
                .ToList(),
            Pagination = ParsePagination(document),
        };
    }

    private static ThreadSummary? ParseRow(IElement row)
    {
        var link = row.QuerySelector("a[id^='thread_title_']");
        if (link is null)
        {
            return null;
        }

        var threadId = ParsingHelpers.IdSuffix(link.Id);
        if (threadId is null)
        {
            return null;
        }

        var href = link.GetAttribute("href");
        var location = href is null ? null : ForumUrls.Parse(href);

        var statusIcon = row.QuerySelector("img[id^='thread_statusicon_']")?.GetAttribute("src") ?? string.Empty;
        var counters = ParseCounters(row);

        return new ThreadSummary
        {
            ThreadId = threadId.Value,
            Title = link.CleanText(),
            ForumSlug = location?.Slug ?? string.Empty,
            Preview = ExtractPreview(row),
            Replies = counters.Replies,
            Views = counters.Views,
            PageCount = CountPages(row, threadId.Value),
            IsSticky = row.QuerySelector("img[src*='sticky']") is not null,
            IsClosed = statusIcon.Contains("lock", StringComparison.OrdinalIgnoreCase),
            HasNewPosts = statusIcon.Contains("_new", StringComparison.OrdinalIgnoreCase),
            LastPostAt = ExtractLastPostDate(row),
        };
    }

    // Счётчики берём из title соседней ячейки («Ответов: 12, просмотров: 4,374») —
    // он есть в обеих вёрстках, в отличии от отдельных колонок.
    private static (int Replies, int Views) ParseCounters(IElement row)
    {
        foreach (var cell in row.QuerySelectorAll("td[title]"))
        {
            var match = CountersRegex().Match(cell.GetAttribute("title") ?? string.Empty);
            if (match.Success)
            {
                return (
                    ParsingHelpers.ParseCount(match.Groups["replies"].Value),
                    ParsingHelpers.ParseCount(match.Groups["views"].Value));
            }
        }

        // в мобильной это подпись под темой: «29.04.2026 / Ответов: 12»
        foreach (var element in row.QuerySelectorAll("div.smallfont"))
        {
            var match = RepliesOnlyRegex().Match(element.TextContent);
            if (match.Success)
            {
                return (ParsingHelpers.ParseCount(match.Groups["replies"].Value), 0);
            }
        }

        return (0, 0);
    }

    // короткая выжимка первого сообщения, форум показывает её под заголовком
    private static string? ExtractPreview(IElement row)
    {
        foreach (var element in row.QuerySelectorAll("div.smallfont"))
        {
            var text = element.CleanText();

            if (text.Length == 0 || element.QuerySelector("a") is not null)
            {
                continue;
            }

            // строку с датой и счётчиком за превью не принимаем
            if (DateLineRegex().IsMatch(text))
            {
                continue;
            }

            return text;
        }

        return null;
    }

    private static DateTimeOffset? ExtractLastPostDate(IElement row)
    {
        foreach (var element in row.QuerySelectorAll("div.smallfont"))
        {
            var match = DateLineRegex().Match(element.TextContent);
            if (!match.Success)
            {
                continue;
            }

            var time = element.QuerySelector("span.time")?.TextContent
                       ?? TimeInTextRegex().Match(element.TextContent).Value;

            return ParsingHelpers.ParseDateTime(match.Value, time);
        }

        return null;
    }

    // сколько страниц в самой теме — считаем по ссылкам «2 3 4» рядом с заголовком
    private static int CountPages(IElement row, int threadId)
    {
        var max = 1;

        foreach (var anchor in row.QuerySelectorAll("a[href*='thread']"))
        {
            var location = ForumUrls.Parse(anchor.GetAttribute("href") ?? string.Empty);
            if (location is { Kind: ForumUrlKind.Thread } && location.ThreadId == threadId && location.Page > max)
            {
                max = location.Page;
            }
        }

        return max;
    }

    // пагинация раздела: «1 из 166» в шапке блока страниц
    private static Pagination ParsePagination(IDocument document)
    {
        var control = document.QuerySelector("div.pagenav td.vbmenu_control");
        var match = PaginationRegex().Match(control?.TextContent ?? string.Empty);

        if (match.Success)
        {
            return new Pagination
            {
                CurrentPage = int.Parse(match.Groups["current"].Value),
                PageCount = int.Parse(match.Groups["total"].Value),
            };
        }

        return new Pagination();
    }

    // Числовой id раздела нужен для ленты. На самой странице его нет, но он торчит
    // в ссылках «Новая тема» (newthread.php?f=129) и «Карта этого раздела» (map.php?f=129).
    private static int? ExtractForumId(IDocument document)
    {
        foreach (var anchor in document.QuerySelectorAll("a[href*='f=']"))
        {
            var href = anchor.GetAttribute("href") ?? string.Empty;

            if (!href.Contains("newthread.php", StringComparison.OrdinalIgnoreCase) &&
                !href.Contains("map.php", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = ForumIdRegex().Match(href);
            if (match.Success && int.TryParse(match.Groups[1].Value, out var id) && id > 0)
            {
                return id;
            }
        }

        return null;
    }

    [GeneratedRegex(@"Ответов:\s*(?<replies>[\d\s, ]+),\s*просмотров:\s*(?<views>[\d\s, ]+)", RegexOptions.IgnoreCase)]
    private static partial Regex CountersRegex();

    [GeneratedRegex(@"Ответов:\s*(?<replies>[\d\s, ]+)", RegexOptions.IgnoreCase)]
    private static partial Regex RepliesOnlyRegex();

    [GeneratedRegex(@"\d{2}\.\d{2}\.\d{4}|Сегодня|Вчера", RegexOptions.IgnoreCase)]
    private static partial Regex DateLineRegex();

    [GeneratedRegex(@"\d{1,2}:\d{2}")]
    private static partial Regex TimeInTextRegex();

    [GeneratedRegex(@"(?<current>\d+)\s+из\s+(?<total>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PaginationRegex();

    [GeneratedRegex(@"[?&]f=(-?\d+)")]
    private static partial Regex ForumIdRegex();
}
