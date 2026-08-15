using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using CyberForum.Core.Models;

namespace CyberForum.Core.Parsing;

/// <summary>
/// Страница темы разбирается по родным id движка: тело сообщения всегда лежит
/// в div#post_message_{postId}, а вокруг него таблица #post{postId} с автором,
/// датой и номером. Это самая устойчивая опора на такой вёрстке.
/// </summary>
public sealed partial class ThreadPageParser
{
    private readonly HtmlParser _parser = new();

    public ThreadView Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        using var document = _parser.ParseDocument(html);

        var posts = document
            .QuerySelectorAll("div[id^='post_message_']")
            .Select(ParsePost)
            .OfType<ForumPost>()
            .ToList();

        var (threadId, forumSlug) = FindThreadReference(document);

        return new ThreadView
        {
            ThreadId = threadId,
            Title = ExtractTitle(document),
            ForumSlug = forumSlug,
            ForumTitle = ExtractForumTitle(document, forumSlug),
            Posts = posts,
            Pagination = ParsePagination(document),
        };
    }

    private static ForumPost? ParsePost(IElement message)
    {
        var postId = ParsingHelpers.IdSuffix(message.Id);
        if (postId is null)
        {
            return null;
        }

        // Контейнер — таблица #post{id}. Если вёрстку однажды поменяют, откатываемся
        // на ближайшую таблицу: лучше потерять аватарку, чем всё сообщение.
        var container = message.Closest($"table#post{postId}")
                        ?? message.Closest("table[id^='post']")
                        ?? message.ParentElement
                        ?? message;

        var authorLink = container.QuerySelector("a.bigusername");
        var memberLink = container.QuerySelector("a[href*='/members/']");

        return new ForumPost
        {
            PostId = postId.Value,
            Number = ExtractNumber(container, postId.Value),
            Author = ExtractAuthor(authorLink),
            AuthorId = ExtractMemberId(memberLink),
            AvatarUrl = ForumUrls.Absolute(
                container.QuerySelector("img[src*='customavatars']")?.GetAttribute("src"))?.ToString(),
            PostedAt = ExtractDate(container),
            ContentHtml = message.InnerHtml.Trim(),
            IsBestAnswer = container.QuerySelector("img[src*='tick.png'], .bestanswer") is not null,
            Attachments = ExtractAttachments(message),
        };
    }

    // Внутри ссылки с ником прячется служебный span с «собакой» для упоминаний —
    // в имя его тащить не надо.
    private static string ExtractAuthor(IElement? authorLink)
    {
        if (authorLink is null)
        {
            return string.Empty;
        }

        var clone = (IElement)authorLink.Clone(true);
        foreach (var hidden in clone.QuerySelectorAll("span[id^='tagg_']"))
        {
            hidden.Remove();
        }

        return clone.CleanText().TrimStart('@');
    }

    private static int? ExtractMemberId(IElement? memberLink)
    {
        var match = MemberIdRegex().Match(memberLink?.GetAttribute("href") ?? string.Empty);
        return match.Success ? int.Parse(match.Groups[1].Value) : null;
    }

    // номер сообщения в теме живёт в ссылке-якоре справа от даты
    private static int? ExtractNumber(IElement container, int postId)
    {
        var anchor = container.QuerySelector($"a[href*='#post{postId}']");
        var text = anchor.CleanText();

        return int.TryParse(text, out var number) ? number : null;
    }

    // дата сообщения — «16.08.2025, 17:22» в строке над телом
    private static DateTimeOffset? ExtractDate(IElement container)
    {
        foreach (var cell in container.QuerySelectorAll("td.smallfont, div.smallfont"))
        {
            var match = DateTimeRegex().Match(cell.TextContent);
            if (match.Success)
            {
                return ParsingHelpers.ParseDateTime(match.Groups["date"].Value, match.Groups["time"].Value);
            }
        }

        return null;
    }

    private static IReadOnlyList<PostAttachment> ExtractAttachments(IElement message)
    {
        var attachments = new List<PostAttachment>();

        foreach (var anchor in message.QuerySelectorAll("a[href*='attachment.php']"))
        {
            var url = ForumUrls.Absolute(anchor.GetAttribute("href"))?.ToString();
            if (url is null)
            {
                continue;
            }

            attachments.Add(new PostAttachment
            {
                Url = url,
                Name = anchor.CleanText() is { Length: > 0 } name ? name : null,
                IsImage = anchor.QuerySelector("img") is not null,
            });
        }

        return attachments;
    }

    private static string ExtractTitle(IDocument document)
    {
        var heading = document.QuerySelector("h1.content") ?? document.QuerySelector("h1");
        var title = heading.CleanText();

        if (title.Length > 0)
        {
            return title;
        }

        // запасной путь — заголовок вкладки вида «Тема - Раздел - Киберфорум»
        var documentTitle = ParsingHelpers.CleanText(document.Title);
        var separator = documentTitle.IndexOf(" - ", StringComparison.Ordinal);

        return separator > 0 ? documentTitle[..separator] : documentTitle;
    }

    // Берём любую ссылку на саму тему — в ней сразу и id темы, и слаг раздела.
    // Такая ссылка есть у каждого сообщения, это якорь с его номером.
    private static (int ThreadId, string? ForumSlug) FindThreadReference(IDocument document)
    {
        foreach (var anchor in document.QuerySelectorAll("a[href*='thread']"))
        {
            var location = ForumUrls.Parse(anchor.GetAttribute("href") ?? string.Empty);
            if (location is { Kind: ForumUrlKind.Thread, ThreadId: { } id })
            {
                return (id, location.Slug);
            }
        }

        return (0, null);
    }

    private static string? ExtractForumTitle(IDocument document, string? forumSlug)
    {
        if (forumSlug is null)
        {
            return null;
        }

        foreach (var anchor in document.QuerySelectorAll("a[href*='cyberforum.ru']"))
        {
            var location = ForumUrls.Parse(anchor.GetAttribute("href") ?? string.Empty);
            if (location is { Kind: ForumUrlKind.Forum } && location.Slug == forumSlug)
            {
                var text = anchor.CleanText();
                if (text.Length > 0)
                {
                    return text;
                }
            }
        }

        return null;
    }

    private static Pagination ParsePagination(IDocument document)
    {
        var control = document.QuerySelector("div.pagenav td.vbmenu_control");
        var match = PaginationRegex().Match(control?.TextContent ?? string.Empty);

        return match.Success
            ? new Pagination
            {
                CurrentPage = int.Parse(match.Groups["current"].Value),
                PageCount = int.Parse(match.Groups["total"].Value),
            }
            : new Pagination();
    }

    [GeneratedRegex(@"/members/(\d+)")]
    private static partial Regex MemberIdRegex();

    [GeneratedRegex(@"(?<date>\d{2}\.\d{2}\.\d{4}|Сегодня|Вчера),?\s*(?<time>\d{1,2}:\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex DateTimeRegex();

    [GeneratedRegex(@"(?<current>\d+)\s+из\s+(?<total>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex PaginationRegex();
}
