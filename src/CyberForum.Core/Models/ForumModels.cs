namespace CyberForum.Core.Models;

// Раздел форума. Дерево собираем из карты (map.php) — другого места, где есть всё сразу, нет.
public sealed record ForumNode
{
    // слаг из адреса: python для /python/
    public required string Slug { get; init; }

    public required string Title { get; init; }

    public string? Description { get; init; }

    // Числовой id движка. В карте его нет, подтягиваем со страницы раздела — нужен только для RSS.
    public int? ForumId { get; init; }

    public IReadOnlyList<ForumNode> Children { get; init; } = [];

    public bool HasChildren => Children.Count > 0;
}

// Тема в списке тем раздела
public sealed record ThreadSummary
{
    public required int ThreadId { get; init; }

    public required string Title { get; init; }

    // слаг раздела берём из ссылки самой темы, а не из того, куда мы зашли
    public required string ForumSlug { get; init; }

    public string? Author { get; init; }

    public string? Preview { get; init; }

    public int Replies { get; init; }

    public int Views { get; init; }

    public int PageCount { get; init; } = 1;

    public bool IsSticky { get; init; }

    public bool IsClosed { get; init; }

    public bool HasNewPosts { get; init; }

    public DateTimeOffset? LastPostAt { get; init; }

    public string? LastPostAuthor { get; init; }
}

// Одно сообщение внутри темы
public sealed record ForumPost
{
    public required int PostId { get; init; }

    // порядковый номер в теме («#12»), если форум его показал
    public int? Number { get; init; }

    public required string Author { get; init; }

    public int? AuthorId { get; init; }

    public string? AuthorTitle { get; init; }

    public string? AvatarUrl { get; init; }

    public DateTimeOffset? PostedAt { get; init; }

    // сырой html тела, как отдал форум — причёсываем позже, в санитайзере
    public required string ContentHtml { get; init; }

    public string? Signature { get; init; }

    public bool IsBestAnswer { get; init; }

    public int Likes { get; init; }

    public IReadOnlyList<PostAttachment> Attachments { get; init; } = [];
}

public sealed record PostAttachment
{
    public required string Url { get; init; }

    public string? Name { get; init; }

    public bool IsImage { get; init; }
}

public sealed record Pagination
{
    public int CurrentPage { get; init; } = 1;

    public int PageCount { get; init; } = 1;

    public bool HasNext => CurrentPage < PageCount;

    public bool HasPrevious => CurrentPage > 1;
}

// Одна страница списка тем
public sealed record ForumListing
{
    public required string Slug { get; init; }

    public string? Title { get; init; }

    public int? ForumId { get; init; }

    public IReadOnlyList<ThreadSummary> Threads { get; init; } = [];

    public IReadOnlyList<ForumNode> Subforums { get; init; } = [];

    public Pagination Pagination { get; init; } = new();
}

// Одна страница темы
public sealed record ThreadView
{
    public required int ThreadId { get; init; }

    public required string Title { get; init; }

    public string? ForumSlug { get; init; }

    public string? ForumTitle { get; init; }

    public IReadOnlyList<ForumPost> Posts { get; init; } = [];

    public Pagination Pagination { get; init; } = new();
}

// Элемент RSS-ленты. Лента хороша тем, что отдаёт первое сообщение целиком.
public sealed record FeedItem
{
    public required int ThreadId { get; init; }

    public required string Title { get; init; }

    public required string Link { get; init; }

    public string? ForumSlug { get; init; }

    public string? ForumTitle { get; init; }

    public string? Author { get; init; }

    public string? Summary { get; init; }

    public string? ContentHtml { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    // из RSS их не узнать, зато они есть в «свежих темах» для вошедших
    public int Views { get; init; }

    public int Replies { get; init; }

    public bool HasCounters => Views > 0 || Replies > 0;

    public string AuthorLine => string.IsNullOrEmpty(Author) ? string.Empty : "✍ " + Author;

    public string ViewsLine => "👁 " + Short(Views);

    public string RepliesLine => "💬 " + Short(Replies);

    // 940, 4к, 4.2к, 1.3м — длинные числа на карточке ни к чему
    private static string Short(int value) => value switch
    {
        < 1000 => value.ToString(),
        < 10000 => (value / 1000d).ToString("0.#").Replace(',', '.') + "к",
        < 1000000 => (value / 1000).ToString() + "к",
        _ => (value / 1000000d).ToString("0.#").Replace(',', '.') + "м",
    };
}

// Кто мы для форума прямо сейчас
public sealed record SessionState
{
    public bool IsAuthenticated { get; init; }

    public string? UserName { get; init; }

    public int? UserId { get; init; }

    // токен из скрытых полей форм, у гостя там просто guest
    public string? SecurityToken { get; init; }
}
