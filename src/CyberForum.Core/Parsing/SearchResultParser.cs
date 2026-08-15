using AngleSharp.Html.Parser;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

/// <summary>
/// Разбирает выдачу поиска. Движок показывает её тем же списком тем, что и раздел,
/// поэтому разбор переиспользуем — отличается только то, что темы тут из разных разделов.
/// </summary>
public sealed class SearchResultParser
{
    private readonly HtmlParser _parser = new();
    private readonly ThreadListParser _listParser = new();

    public IReadOnlyList<ThreadSummary> Parse(string html)
    {
        var listing = _listParser.Parse(html, string.Empty);

        if (listing.Threads.Count > 0)
        {
            return listing.Threads;
        }

        // Запасной путь на случай, если выдача свёрстана не строками таблицы:
        // собираем всё, что похоже на ссылку на тему.
        using var document = _parser.ParseDocument(html);

        var found = new List<ThreadSummary>();
        var seen = new HashSet<int>();

        foreach (var anchor in document.QuerySelectorAll("a[id^='thread_title_'], a[href*='thread']"))
        {
            var location = ForumUrls.Parse(anchor.GetAttribute("href") ?? string.Empty);

            if (location is not { Kind: ForumUrlKind.Thread, ThreadId: { } id } || !seen.Add(id))
            {
                continue;
            }

            var title = anchor.CleanText();

            if (title.Length == 0)
            {
                continue;
            }

            found.Add(new ThreadSummary
            {
                ThreadId = id,
                Title = title,
                ForumSlug = location.Slug ?? string.Empty,
            });
        }

        return found;
    }
}
