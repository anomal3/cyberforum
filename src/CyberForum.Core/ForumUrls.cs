using System.Text.RegularExpressions;

namespace CyberForum.Core;

public enum ForumUrlKind
{
    Unknown,
    Home,
    Forum,
    Thread,
    Post,
    Member,
    Blog,
}

// Разобранный адрес форума
public sealed record ForumLocation
{
    public required ForumUrlKind Kind { get; init; }

    public string? Slug { get; init; }

    public int? ThreadId { get; init; }

    public int? PostId { get; init; }

    public int Page { get; init; } = 1;
}

/// <summary>
/// Единственное место, где живут адреса форума. Ходим только по ЧПУ от vBSEO
/// (/python/thread123.html). Родные адреса движка — showthread.php?t=, forumdisplay.php?f= —
/// закрыты и отдают 403, так что строить их нельзя вообще никогда.
/// </summary>
public static partial class ForumUrls
{
    public const string Host = "www.cyberforum.ru";

    public static readonly Uri Base = new($"https://{Host}/");

    // отсюда форум раздаёт статику: аватарки, смайлы, иконки
    public const string StaticHost = "cyberstatic.net";

    public static Uri Home() => Base;

    // список тем: /python/, а со второй страницы уже /python-page2.html
    public static Uri Forum(string slug, int page = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        slug = slug.Trim('/');
        return page <= 1
            ? new Uri(Base, $"{slug}/")
            : new Uri(Base, $"{slug}-page{page}.html");
    }

    public static Uri Thread(string slug, int threadId, int page = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threadId);
        slug = slug.Trim('/');
        return page <= 1
            ? new Uri(Base, $"{slug}/thread{threadId}.html")
            : new Uri(Base, $"{slug}/thread{threadId}-page{page}.html");
    }

    // лента всего форума или одного раздела, отдаёт первые сообщения целиком
    public static Uri Rss(int? forumId = null) =>
        forumId is null
            ? new Uri(Base, "external.php?type=RSS2")
            : new Uri(Base, $"external.php?type=RSS2&forumids={forumId}");

    public static Uri Map() => new(Base, "map.php");

    // страницу входа открываем в WebView — там reCAPTCHA, обычным клиентом не пройти
    public static Uri Login() => new(Base, "auth.php");

    public static Uri Register() => new(Base, "register.php");

    // форма входа постится сюда, а не на auth.php, где её показывают
    public static Uri LoginPost() => new(Base, "posting.php?do=login");

    // личный кабинет: уведомления, отмеченные ответы, закладки, отзывы
    public static Uri UserCp() => new(Base, "usercp.php");

    public static Uri Member(int userId) => new(Base, $"members/{userId}.html");

    public static Uri Blog(int userId) => new(Base, $"blogs/{userId}/");

    public static Uri BlogEntry(int userId, int entryId) => new(Base, $"blogs/{userId}/{entryId}.html");

    // страницы форума, которые мы не переписываем, а показываем как есть
    public static Uri EditProfile() => new(Base, "profile.php?do=editprofile");

    public static Uri BlogCabinet() => new(Base, "blog_usercp.php");

    public static Uri Search() => new(Base, "search.php");

    // «Свежие темы» движка: в отличие от RSS тут есть просмотры, ответы и автор
    public static Uri Daily() => new(Base, "search.php?do=getdaily");

    // форма поиска постится именно сюда, do=process в адресе, а не только в полях
    public static Uri SearchProcess() => new(Base, "search.php?do=process");

    public static Uri PrivateMessages() => new(Base, "private.php");

    public static Uri UserControlPanel() => new(Base, "usercp.php");

    public static Uri? Absolute(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        href = href.Trim();

        // ссылки на статику приходят без протокола: //cyberstatic.net/...
        if (href.StartsWith("//", StringComparison.Ordinal))
        {
            return new Uri($"https:{href}");
        }

        return Uri.TryCreate(Base, href, out var uri) ? uri : null;
    }

    public static bool IsForumHost(Uri uri) =>
        uri.Host.Equals(Host, StringComparison.OrdinalIgnoreCase) ||
        uri.Host.Equals("cyberforum.ru", StringComparison.OrdinalIgnoreCase);

    // Обратная операция: из адреса достаём раздел, тему и страницу. Нужна и для ссылок
    // внутри сообщений, и чтобы восстанавливать состояние при возврате в приложение.
    public static ForumLocation Parse(string url)
    {
        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        {
            return new ForumLocation { Kind = ForumUrlKind.Unknown };
        }

        if (!uri.IsAbsoluteUri)
        {
            uri = new Uri(Base, uri);
        }

        if (!IsForumHost(uri))
        {
            return new ForumLocation { Kind = ForumUrlKind.Unknown };
        }

        var path = uri.AbsolutePath.Trim('/');

        if (path.Length == 0)
        {
            return new ForumLocation { Kind = ForumUrlKind.Home };
        }

        var thread = ThreadPathRegex().Match(path);
        if (thread.Success)
        {
            return new ForumLocation
            {
                Kind = ForumUrlKind.Thread,
                Slug = thread.Groups["slug"].Value,
                ThreadId = int.Parse(thread.Groups["id"].Value),
                Page = thread.Groups["page"].Success ? int.Parse(thread.Groups["page"].Value) : 1,
            };
        }

        // ссылка на конкретное сообщение: /python/post12345.html
        var post = PostPathRegex().Match(path);
        if (post.Success)
        {
            return new ForumLocation
            {
                Kind = ForumUrlKind.Post,
                Slug = post.Groups["slug"].Value,
                PostId = int.Parse(post.Groups["id"].Value),
            };
        }

        var forumPage = ForumPageRegex().Match(path);
        if (forumPage.Success)
        {
            return new ForumLocation
            {
                Kind = ForumUrlKind.Forum,
                Slug = forumPage.Groups["slug"].Value,
                Page = int.Parse(forumPage.Groups["page"].Value),
            };
        }

        if (path.StartsWith("blogs", StringComparison.OrdinalIgnoreCase))
        {
            return new ForumLocation { Kind = ForumUrlKind.Blog, Slug = path };
        }

        if (path.StartsWith("members", StringComparison.OrdinalIgnoreCase))
        {
            return new ForumLocation { Kind = ForumUrlKind.Member, Slug = path };
        }

        if (SlugRegex().IsMatch(path))
        {
            return new ForumLocation { Kind = ForumUrlKind.Forum, Slug = path };
        }

        return new ForumLocation { Kind = ForumUrlKind.Unknown };
    }

    public static int? ThreadIdFromUrl(string? url) =>
        url is null ? null : Parse(url) is { Kind: ForumUrlKind.Thread, ThreadId: { } id } ? id : null;

    [GeneratedRegex(@"^(?<slug>[a-z0-9\-]+)/thread(?<id>\d+)(?:-page(?<page>\d+))?\.html$", RegexOptions.IgnoreCase)]
    private static partial Regex ThreadPathRegex();

    [GeneratedRegex(@"^(?<slug>[a-z0-9\-]+)/post(?<id>\d+)\.html$", RegexOptions.IgnoreCase)]
    private static partial Regex PostPathRegex();

    [GeneratedRegex(@"^(?<slug>[a-z0-9\-]+)-page(?<page>\d+)\.html$", RegexOptions.IgnoreCase)]
    private static partial Regex ForumPageRegex();

    [GeneratedRegex(@"^[a-z0-9\-]+$", RegexOptions.IgnoreCase)]
    private static partial Regex SlugRegex();
}
