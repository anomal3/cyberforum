using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.Core;
using CyberForum.Core.Models;

namespace CyberForum.App.ViewModels;

// Главная вкладка: свежие темы со всего форума. Берём из RSS — он лёгкий
// и отдаёт первое сообщение целиком, так что превью получается настоящее.
public sealed partial class FeedViewModel(ForumClient client) : BaseViewModel
{
    private IReadOnlyDictionary<string, string>? _names;

    public ObservableCollection<FeedItem> Items { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async token =>
    {
        if (Items.Count > 0)
        {
            return;
        }

        Title = "Свежее";
        await FillAsync(token);
    });

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(FillAsync);

    [RelayCommand]
    private static Task OpenAsync(FeedItem? item) =>
        item is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToThread(item.ForumSlug ?? string.Empty, item.ThreadId, item.Title));

    /// <summary>
    /// Вошедшим показываем «свежие темы» самого форума: там есть просмотры, ответы
    /// и автор. Гостю эта страница закрыта капчей, ему остаётся RSS — он легче,
    /// отдаёт превью целиком, но счётчиков в нём нет вовсе.
    /// </summary>
    private async Task FillAsync(CancellationToken token)
    {
        var items = await FreshAsync(token);

        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }
    }

    private async Task<IReadOnlyList<FeedItem>> FreshAsync(CancellationToken token)
    {
        try
        {
            var daily = await client.GetDailyAsync(token);

            if (daily.Count > 0)
            {
                // в «свежих темах» раздел указан только слагом, а человеку нужно имя
                var names = await NamesAsync(token);

                return daily.Select(thread => Convert(thread, names)).ToList();
            }
        }
        catch (Exception)
        {
            // не пустил — не беда, ниже возьмём ленту
        }

        return await client.GetFeedAsync(token: token);
    }

    private async Task<IReadOnlyDictionary<string, string>> NamesAsync(CancellationToken token)
    {
        if (_names is not null)
        {
            return _names;
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in await client.GetForumTreeAsync(token))
        {
            Collect(category, names);
        }

        return _names = names;
    }

    private static void Collect(ForumNode node, Dictionary<string, string> names)
    {
        names[node.Slug] = node.Title;

        foreach (var child in node.Children)
        {
            Collect(child, names);
        }
    }

    private static FeedItem Convert(ThreadSummary thread, IReadOnlyDictionary<string, string> names) => new()
    {
        ThreadId = thread.ThreadId,
        Title = thread.Title,
        Link = ForumUrls.Thread(thread.ForumSlug, thread.ThreadId).ToString(),
        ForumSlug = thread.ForumSlug,
        ForumTitle = names.GetValueOrDefault(thread.ForumSlug, thread.ForumSlug),
        Author = thread.Author ?? thread.LastPostAuthor,
        Summary = thread.Preview,
        PublishedAt = thread.LastPostAt,
        Views = thread.Views,
        Replies = thread.Replies,
    };
}
