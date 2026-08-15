using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.Core;
using CyberForum.Core.Storage;

namespace CyberForum.App.ViewModels;

public sealed partial class ThreadListViewModel(ForumClient client, CacheStore cache)
    : BaseViewModel, IQueryAttributable
{
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        Slug = Read(query, "slug");
        SectionTitle = Read(query, "title");
        Title = SectionTitle;
    }

    private static string Read(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) ? Uri.UnescapeDataString(value?.ToString() ?? string.Empty) : string.Empty;

    private int _lastLoadedPage;
    private int _pageCount = 1;

    public ObservableCollection<ThreadRow> Threads { get; } = [];

    [ObservableProperty]
    public partial string Slug { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SectionTitle { get; set; } = string.Empty;

    public bool CanLoadMore => _lastLoadedPage < _pageCount;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async token =>
    {
        if (Threads.Count > 0)
        {
            // вернулись из темы — счётчики прочитанного могли поменяться
            await RefreshMarksAsync();
            return;
        }

        Title = SectionTitle;
        await LoadPageAsync(1, reset: true, token);
    });

    [RelayCommand]
    private Task RefreshAsync() => RunAsync(token => LoadPageAsync(1, reset: true, token));

    // Подгрузка следующей страницы, когда человек долистал до низа
    [RelayCommand]
    private Task LoadMoreAsync() => RunAsync(async token =>
    {
        if (!CanLoadMore)
        {
            return;
        }

        await LoadPageAsync(_lastLoadedPage + 1, reset: false, token);
    });

    [RelayCommand]
    private async Task OpenAsync(ThreadRow? row)
    {
        if (row is null)
        {
            return;
        }

        var thread = row.Thread;
        var slug = string.IsNullOrEmpty(thread.ForumSlug) ? Slug : thread.ForumSlug;

        // запоминаем, сколько ответов было на момент захода: со следующего раза
        // всё сверх этого числа и будет «новым»
        await cache.RememberPositionAsync(new ThreadReadState
        {
            ThreadId = thread.ThreadId,
            Slug = slug,
            Title = thread.Title,
            Replies = thread.Replies,
        });

        await Shell.Current.GoToAsync(Routes.ToThread(slug, thread.ThreadId, thread.Title));
    }

    private async Task LoadPageAsync(int page, bool reset, CancellationToken token)
    {
        var listing = await client.GetListingAsync(Slug, page, token);
        var states = await cache.GetStatesAsync();

        if (reset)
        {
            Threads.Clear();
        }

        foreach (var thread in listing.Threads)
        {
            Threads.Add(new ThreadRow(thread, states.GetValueOrDefault(thread.ThreadId)));
        }

        _lastLoadedPage = listing.Pagination.CurrentPage;
        _pageCount = listing.Pagination.PageCount;

        if (!string.IsNullOrEmpty(listing.Title))
        {
            Title = listing.Title;
        }

        OnPropertyChanged(nameof(CanLoadMore));
    }

    // Пересобираем строки на месте: сеть не трогаем, меняются только наши отметки.
    private async Task RefreshMarksAsync()
    {
        var states = await cache.GetStatesAsync();
        var threads = Threads.Select(row => row.Thread).ToList();

        Threads.Clear();

        foreach (var thread in threads)
        {
            Threads.Add(new ThreadRow(thread, states.GetValueOrDefault(thread.ThreadId)));
        }
    }
}
