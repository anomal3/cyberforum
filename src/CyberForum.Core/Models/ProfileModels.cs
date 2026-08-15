namespace CyberForum.Core.Models;

/// <summary>
/// Личный кабинет вошедшего: то, что форум показывает на usercp.php.
/// </summary>
public sealed record UserCabinet
{
    public int? UserId { get; init; }

    public string? UserName { get; init; }

    public int Notifications { get; init; }

    public int NewMessages { get; init; }

    public int TotalMessages { get; init; }

    public int ReputationTotal { get; init; }

    public int BestAnswersTotal { get; init; }

    // ответы, которые отметили лучшими
    public IReadOnlyList<PostRef> BestAnswers { get; init; } = [];

    // сообщения, которые человек положил себе в закладки
    public IReadOnlyList<PostRef> Bookmarks { get; init; } = [];

    public IReadOnlyList<ReputationNote> Reputation { get; init; } = [];

    // что именно форум считает уведомлениями: упоминания, личные сообщения и прочее
    public IReadOnlyList<Notice> Notices { get; init; } = [];
}

public sealed record Notice(string Title, int Count, string Url);

/// <summary>Ссылка на конкретное сообщение в теме.</summary>
public sealed record PostRef
{
    public required string Title { get; init; }

    public string? ThreadTitle { get; init; }

    public string? ForumTitle { get; init; }

    public required string ForumSlug { get; init; }

    public int ThreadId { get; init; }

    public int PostId { get; init; }

    public string? Author { get; init; }

    public DateTimeOffset? At { get; init; }
}

/// <summary>Отзыв: балл, за что и от кого.</summary>
public sealed record ReputationNote
{
    public required string ThreadTitle { get; init; }

    public required string ForumSlug { get; init; }

    public int ThreadId { get; init; }

    public int PostId { get; init; }

    public string? Author { get; init; }

    public string? Comment { get; init; }

    public string Points { get; init; } = string.Empty;

    public DateTimeOffset? At { get; init; }
}

/// <summary>Публичный профиль: то, что форум показывает на странице участника.</summary>
public sealed record MemberProfile
{
    public int UserId { get; init; }

    public required string UserName { get; init; }

    public string? AvatarUrl { get; init; }

    public string? LastActivity { get; init; }

    public IReadOnlyList<ProfileField> Stats { get; init; } = [];

    public IReadOnlyList<ProfileField> About { get; init; } = [];

    public IReadOnlyList<BlogEntry> Blog { get; init; } = [];
}

public sealed record ProfileField(string Name, string Value);

/// <summary>Запись блога — и в списке, и открытая целиком.</summary>
public sealed record BlogPost
{
    public int EntryId { get; init; }

    public int UserId { get; init; }

    public required string Title { get; init; }

    public string Url { get; init; } = string.Empty;

    public string? Author { get; init; }

    public string? When { get; init; }

    public string? Preview { get; init; }

    // тело есть только у открытой записи
    public string? BodyHtml { get; init; }

    public int Views { get; init; }

    public int Comments { get; init; }

    public IReadOnlyList<string> Tags { get; init; } = [];

    public string Counters => Comments > 0
        ? $"{Views} просмотров   ·   {Comments} комментариев"
        : $"{Views} просмотров";
}

public sealed record BlogEntry(string Title, string Url, string? When);
