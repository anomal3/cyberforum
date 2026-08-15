using CyberForum.Core.Models;
using CyberForum.Core.Storage;

namespace CyberForum.App.ViewModels;

/// <summary>
/// Строка в простом списке: заголовок, подпись и куда вести. Так одна страница
/// показывает и историю чтения, и отмеченные ответы, и закладки — они отличаются
/// только тем, откуда взялись.
/// </summary>
public sealed class LinkRow
{
    public required string Title { get; init; }

    public string? Subtitle { get; init; }

    public required string Slug { get; init; }

    public int ThreadId { get; init; }

    public int Page { get; init; } = 1;

    public static LinkRow From(ThreadReadState state) => new()
    {
        Title = state.Title,
        Subtitle = Ago(state.SeenAtUtc),
        Slug = state.Slug,
        ThreadId = state.ThreadId,
        Page = state.Page,
    };

    public static LinkRow From(PostRef post) => new()
    {
        Title = string.IsNullOrEmpty(post.ThreadTitle) ? post.Title : post.ThreadTitle,
        Subtitle = Join(post.ForumTitle, post.Author, post.At),
        Slug = post.ForumSlug,
        ThreadId = post.ThreadId,
    };

    private static string Join(string? forum, string? author, DateTimeOffset? at)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(forum))
        {
            parts.Add(forum);
        }

        if (!string.IsNullOrEmpty(author))
        {
            parts.Add(author);
        }

        if (at is not null)
        {
            parts.Add(at.Value.ToLocalTime().ToString("dd.MM.yyyy"));
        }

        return string.Join("   ·   ", parts);
    }

    private static string Ago(DateTime seen) =>
        seen == default ? string.Empty : seen.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
}
