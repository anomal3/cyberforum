using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

/// <summary>
/// Личный кабинет (usercp.php): счётчики сверху, отмеченные ответы, закладки
/// сообщений и полученные отзывы. Всё это форум сваливает на одну страницу,
/// каждый блок — своя таблица со своим id.
/// </summary>
public sealed partial class UserCabinetParser
{
    private readonly HtmlParser _parser = new();

    public UserCabinet Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        using var document = _parser.ParseDocument(html);

        var (userId, userName) = FindUser(document);

        return new UserCabinet
        {
            UserId = userId,
            UserName = userName,
            Notifications = Number(document.QuerySelector("#notifications")?.TextContent),
            NewMessages = Messages(document, first: true),
            TotalMessages = Messages(document, first: false),
            ReputationTotal = Number(FindText(document, "Всего баллов")),
            BestAnswersTotal = Number(FindText(document, "Всего ответов")),
            BestAnswers = BestAnswers(document),
            Bookmarks = Bookmarks(document),
            Reputation = Reputation(document),
            Notices = Notices(document),
        };
    }

    // выпадающее меню колокольчика: слева название, справа число
    private static List<Notice> Notices(IDocument document)
    {
        var found = new List<Notice>();
        var menu = document.QuerySelector("#notifications_menu");

        if (menu is null)
        {
            return found;
        }

        foreach (var row in menu.QuerySelectorAll("tr"))
        {
            var cells = row.QuerySelectorAll("td");

            if (cells.Length < 2)
            {
                continue;
            }

            var title = cells[0].CleanText();
            var count = Number(cells[1].CleanText());
            var link = cells[0].QuerySelector("a[href]")?.GetAttribute("href") ?? string.Empty;

            if (title.Length > 0 && !title.Equals("Уведомления", StringComparison.OrdinalIgnoreCase))
            {
                found.Add(new Notice(title, count, link));
            }
        }

        return found;
    }

    // «Последние отмеченные ответы»: раздел, тема с якорем на сообщение, дата
    private static List<PostRef> BestAnswers(IDocument document)
    {
        var found = new List<PostRef>();
        var body = document.QuerySelector("#collapseobj_usercp_bestanswers");

        if (body is null)
        {
            return found;
        }

        foreach (var row in body.QuerySelectorAll("tr"))
        {
            var cells = row.QuerySelectorAll("td");

            if (cells.Length < 3)
            {
                continue;
            }

            var forum = cells[0].QuerySelector("a[href]");
            var links = cells[1].QuerySelectorAll("a[href]");

            if (links.Length == 0)
            {
                continue;
            }

            var place = ForumUrls.Parse(links[0].GetAttribute("href") ?? string.Empty);

            if (place.Kind != ForumUrlKind.Thread)
            {
                continue;
            }

            found.Add(new PostRef
            {
                Title = links[0].CleanText(),
                ThreadTitle = links[0].CleanText(),
                ForumTitle = forum.CleanText(),
                ForumSlug = place.Slug ?? string.Empty,
                ThreadId = place.ThreadId ?? 0,
                PostId = PostId(links.Length > 1 ? links[1].GetAttribute("href") : null),
                At = When(cells[2].CleanText()),
            });
        }

        return found;
    }

    // «Закладки сообщений»: заголовок сообщения, в скобках тема, ниже автор
    private static List<PostRef> Bookmarks(IDocument document)
    {
        var found = new List<PostRef>();
        var body = document.QuerySelector("#collapseobj_vbfavorites_favposts");

        if (body is null)
        {
            return found;
        }

        foreach (var cell in body.QuerySelectorAll("td.alt1"))
        {
            var links = cell.QuerySelectorAll("a[href]");

            if (links.Length == 0)
            {
                continue;
            }

            var place = ForumUrls.Parse(links[0].GetAttribute("href") ?? string.Empty);

            if (place.Kind != ForumUrlKind.Thread)
            {
                continue;
            }

            var thread = links.Length > 1 ? links[1].CleanText() : null;
            var tail = cell.QuerySelector("span.smallfont:last-of-type").CleanText();
            var author = tail.Split('-', 2)[0].Trim();

            found.Add(new PostRef
            {
                Title = links[0].CleanText(),
                ThreadTitle = string.IsNullOrEmpty(thread) ? links[0].CleanText() : thread,
                ForumSlug = place.Slug ?? string.Empty,
                ThreadId = place.ThreadId ?? 0,
                PostId = PostId(links[0].GetAttribute("href")),
                Author = author.Length is > 0 and < 40 ? author : null,
            });
        }

        return found;
    }

    // «Последние полученные отзывы»: балл, тема, дата, автор, комментарий
    private static List<ReputationNote> Reputation(IDocument document)
    {
        var found = new List<ReputationNote>();
        var body = document.QuerySelector("#collapseobj_usercp_reputation");

        if (body is null)
        {
            return found;
        }

        foreach (var row in body.QuerySelectorAll("tr"))
        {
            var cells = row.QuerySelectorAll("td");

            if (cells.Length < 5)
            {
                continue;
            }

            var link = cells[1].QuerySelector("a[href]");

            if (link is null)
            {
                continue;
            }

            var place = ForumUrls.Parse(link.GetAttribute("href") ?? string.Empty);

            if (place.Kind != ForumUrlKind.Thread)
            {
                continue;
            }

            found.Add(new ReputationNote
            {
                ThreadTitle = link.CleanText(),
                ForumSlug = place.Slug ?? string.Empty,
                ThreadId = place.ThreadId ?? 0,
                PostId = PostId(link.GetAttribute("href")),
                Points = cells[0].CleanText(),
                At = When(cells[2].CleanText()),
                Author = cells[3].CleanText(),
                Comment = cells[4].CleanText(),
            });
        }

        return found;
    }

    // ссылка на свой профиль есть в шапке — из неё и берём номер пользователя
    private static (int? Id, string? Name) FindUser(IDocument document)
    {
        foreach (var anchor in document.QuerySelectorAll("a[href*='member'], a[href*='/members/']"))
        {
            var href = anchor.GetAttribute("href") ?? string.Empty;
            var match = UserIdPattern().Match(href);

            if (match.Success && int.TryParse(match.Groups["id"].Value, out var id))
            {
                var name = anchor.CleanText();

                return (id, name.Length > 0 ? name : null);
            }
        }

        return (null, null);
    }

    private static int Messages(IDocument document, bool first)
    {
        var span = document.QuerySelector(first ? "span[title='Новых']" : "span[title='Всего']");

        return Number(span?.TextContent);
    }

    private static string? FindText(IDocument document, string label)
    {
        foreach (var element in document.QuerySelectorAll("span, td"))
        {
            var text = element.CleanText();

            if (text.StartsWith(label, StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        return null;
    }

    private static int Number(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var match = NumberPattern().Match(text);

        return match.Success && int.TryParse(match.Value, out var value) ? value : 0;
    }

    private static int PostId(string? href)
    {
        if (string.IsNullOrEmpty(href))
        {
            return 0;
        }

        var match = PostPattern().Match(href);

        return match.Success && int.TryParse(match.Groups["id"].Value, out var value) ? value : 0;
    }

    private static DateTimeOffset? When(string text)
    {
        var match = DatePattern().Match(text);

        if (!match.Success)
        {
            return null;
        }

        return ParsingHelpers.ParseDateTime(match.Groups["date"].Value, match.Groups["time"].Value);
    }

    [GeneratedRegex(@"\d+")]
    private static partial Regex NumberPattern();

    [GeneratedRegex(@"post(?<id>\d+)")]
    private static partial Regex PostPattern();

    [GeneratedRegex(@"(?:members/|[?&]u=)(?<id>\d+)")]
    private static partial Regex UserIdPattern();

    [GeneratedRegex(@"(?<date>\d{2}\.\d{2}\.\d{4}|Сегодня|Вчера)[,\s]+(?<time>\d{1,2}:\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex DatePattern();
}
