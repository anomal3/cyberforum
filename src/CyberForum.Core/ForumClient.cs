using CyberForum.Core.Http;
using CyberForum.Core.Models;
using CyberForum.Core.Parsing;
using CyberForum.Core.Storage;

namespace CyberForum.Core;

/// <summary>
/// Всё общение с форумом в одном месте: сходить, разобрать, вернуть модель.
/// Выше этого слоя html уже быть не должно.
/// </summary>
public sealed class ForumClient(ForumHttpClient http, CacheStore? cache = null)
{
    private readonly ForumMapParser _mapParser = new();
    private readonly ForumHomeParser _homeParser = new();
    private readonly UserCabinetParser _cabinetParser = new();
    private readonly MemberProfileParser _memberParser = new();
    private readonly BlogParser _blogParser = new();
    private readonly ThreadListParser _listParser = new();
    private readonly ThreadPageParser _threadParser = new();
    private readonly RssFeedParser _feedParser = new();
    private readonly SessionStateParser _sessionParser = new();

    public ForumHttpClient Http { get; } = http;

    /// <summary>
    /// Дерево разделов. Строим по главной странице: только там видно, что «C# для
    /// начинающих» и «ASP.NET» — это внутренности раздела .NET. Описания разделов
    /// главная не показывает, поэтому дополняем их картой форума. И то и другое
    /// меняется раз в год, так что сутки в кэше им не срок.
    /// </summary>
    public async Task<IReadOnlyList<ForumNode>> GetForumTreeAsync(CancellationToken token = default)
    {
        var home = await LoadAsync(ForumUrls.Home(), null, TimeSpan.FromDays(1), token);
        var tree = _homeParser.Parse(home);

        var map = await LoadAsync(ForumUrls.Map(), null, TimeSpan.FromDays(1), token);
        var described = _mapParser.Parse(map);

        if (tree.Count == 0)
        {
            // главная не разобралась — лучше плоская карта, чем пустой экран
            return described;
        }

        var descriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in described)
        {
            foreach (var section in category.Children)
            {
                if (!string.IsNullOrEmpty(section.Description))
                {
                    descriptions[section.Slug] = section.Description;
                }
            }
        }

        return tree.Select(node => Describe(node, descriptions)).ToList();
    }

    private static ForumNode Describe(ForumNode node, IReadOnlyDictionary<string, string> descriptions)
    {
        var children = node.Children.Count == 0
            ? node.Children
            : node.Children.Select(child => Describe(child, descriptions)).ToList();

        return node with
        {
            Description = node.Description ?? descriptions.GetValueOrDefault(node.Slug),
            Children = children,
        };
    }

    /// <summary>
    /// Личный кабинет. Кэшируем совсем чуть-чуть: там счётчик уведомлений,
    /// и показывать вчерашний смысла нет.
    /// </summary>
    public async Task<UserCabinet> GetCabinetAsync(CancellationToken token = default)
    {
        var html = await LoadAsync(ForumUrls.UserCp(), ForumUrls.Home(), TimeSpan.FromMinutes(2), token);

        return _cabinetParser.Parse(html);
    }

    public async Task<MemberProfile> GetMemberAsync(int userId, CancellationToken token = default)
    {
        var html = await LoadAsync(ForumUrls.Member(userId), ForumUrls.Home(), TimeSpan.FromMinutes(10), token);

        return _memberParser.Parse(html);
    }

    /// <summary>
    /// Свежие темы со счётчиками. Гостю форум эту страницу не отдаёт (там же капча),
    /// поэтому наверху есть запасной путь через RSS.
    /// </summary>
    public async Task<IReadOnlyList<ThreadSummary>> GetDailyAsync(CancellationToken token = default)
    {
        var html = await Http.GetStringAsync(ForumUrls.Daily(), ForumUrls.Home(), token);

        return _listParser.Parse(html, string.Empty).Threads;
    }

    public async Task<IReadOnlyList<BlogPost>> GetBlogAsync(int userId, CancellationToken token = default)
    {
        var html = await LoadAsync(ForumUrls.Blog(userId), ForumUrls.Home(), TimeSpan.FromMinutes(10), token);

        return _blogParser.ParseList(html);
    }

    public async Task<BlogPost?> GetBlogEntryAsync(int userId, int entryId, CancellationToken token = default)
    {
        var html = await LoadAsync(ForumUrls.BlogEntry(userId, entryId), ForumUrls.Blog(userId), TimeSpan.FromHours(1), token);

        return _blogParser.ParseEntry(html);
    }

    public async Task<ForumListing> GetListingAsync(string slug, int page = 1, CancellationToken token = default)
    {
        var html = await LoadAsync(ForumUrls.Forum(slug, page), ForumUrls.Home(), null, token);
        return _listParser.Parse(html, slug);
    }

    public async Task<ThreadView> GetThreadAsync(
        string slug,
        int threadId,
        int page = 1,
        CancellationToken token = default)
    {
        var html = await LoadAsync(ForumUrls.Thread(slug, threadId, page), ForumUrls.Forum(slug), null, token);
        return _threadParser.Parse(html);
    }

    // Лента раздела или всего форума. По трафику дёшево, поэтому годится и для главной,
    // и для проверки «что нового».
    public async Task<IReadOnlyList<FeedItem>> GetFeedAsync(int? forumId = null, CancellationToken token = default)
    {
        var xml = await LoadAsync(ForumUrls.Rss(forumId), null, TimeSpan.FromMinutes(5), token);
        return _feedParser.Parse(xml);
    }

    public async Task<SessionState> GetSessionAsync(CancellationToken token = default)
    {
        var html = await Http.GetStringAsync(ForumUrls.Home(), token: token);
        return _sessionParser.Parse(html);
    }

    /// <summary>
    /// Свежее из кэша отдаём сразу, иначе идём в сеть. Если сеть подвела, а что-то
    /// сохранённое есть — показываем его: в метро лучше вчерашняя тема, чем пустой экран.
    /// </summary>
    private async Task<string> LoadAsync(Uri uri, Uri? referer, TimeSpan? freshFor, CancellationToken token)
    {
        if (cache is not null)
        {
            var stored = await cache.GetPageAsync(uri, freshFor);

            if (stored is not null)
            {
                return stored;
            }
        }

        try
        {
            var body = await Http.GetStringAsync(uri, referer, token);

            if (cache is not null)
            {
                await cache.SavePageAsync(uri, body);
            }

            return body;
        }
        catch (Exception error) when (error is ForumUnavailableException or ForumBlockedException)
        {
            var stale = cache is null ? null : await cache.GetPageAsync(uri, TimeSpan.MaxValue);

            if (stale is not null)
            {
                return stale;
            }

            // стека не теряем — пусть наверх уедет как есть
            throw;
        }
    }
}
