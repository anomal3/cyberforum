using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.App.Services;
using CyberForum.Core.Storage;

namespace CyberForum.App.ViewModels;

/// <summary>
/// Одна страница на три списка: недавно читали, отмеченные ответы и закладки
/// сообщений. Что именно показывать, приходит параметром маршрута.
/// </summary>
public sealed partial class LinkListViewModel(CacheStore cache, CabinetService cabinet)
    : BaseViewModel, IQueryAttributable
{
    private string _kind = "history";

    public ObservableCollection<LinkRow> Rows { get; } = [];

    public string Empty => _kind switch
    {
        "best" => "Пока ни один ваш ответ не отметили лучшим.",
        "bookmarks" => "Закладок сообщений нет.",
        _ => "Пока ничего не открывали.",
    };

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _kind = query.TryGetValue("kind", out var value) ? value?.ToString() ?? "history" : "history";

        Title = _kind switch
        {
            "best" => "Отмеченные ответы",
            "bookmarks" => "Закладки сообщений",
            _ => "Недавно читали",
        };

        OnPropertyChanged(nameof(Empty));

        await FillAsync();
    }

    [RelayCommand]
    private Task FillAsync() => RunAsync(async _ =>
    {
        Rows.Clear();

        if (_kind == "history")
        {
            foreach (var state in await cache.GetHistoryAsync(100))
            {
                Rows.Add(LinkRow.From(state));
            }

            return;
        }

        if (!cabinet.Loaded)
        {
            await cabinet.RefreshAsync();
        }

        var posts = _kind == "best" ? cabinet.Current.BestAnswers : cabinet.Current.Bookmarks;

        foreach (var post in posts)
        {
            Rows.Add(LinkRow.From(post));
        }
    });

    [RelayCommand]
    private static Task OpenAsync(LinkRow? row) =>
        row is null || row.ThreadId <= 0
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToThread(row.Slug, row.ThreadId, row.Title, row.Page));
}
