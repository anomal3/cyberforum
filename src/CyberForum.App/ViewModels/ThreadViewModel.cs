using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.Core;
using CyberForum.Core.Storage;

namespace CyberForum.App.ViewModels;

/// <summary>
/// Тема показывается не собранным у нас документом, а живой страницей форума в WebView:
/// к страницам тем форум пускает только настоящий браузер, обычному http-клиенту
/// отвечает «Нет доступа». Причёсываем её уже на месте, скриптом-читалкой.
/// </summary>
public sealed partial class ThreadViewModel(CacheStore cache) : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    public partial string Slug { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int ThreadId { get; set; }

    [ObservableProperty]
    public partial int Page { get; set; } = 1;

    [ObservableProperty]
    public partial string ThreadTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Source { get; set; } = string.Empty;

    // Параметры разбираем руками: Shell отдаёт их строками, а QueryProperty
    // на int-свойствах от этого молча ничего не присваивает.
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        Slug = Read(query, "slug");
        ThreadTitle = Read(query, "title");
        ThreadId = ReadNumber(query, "id");
        Page = Math.Max(1, ReadNumber(query, "page"));

        Title = ThreadTitle;
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async _ =>
    {
        if (ThreadId <= 0 || string.IsNullOrEmpty(Slug))
        {
            ErrorMessage = "Не понял, какую тему открывать.";
            return;
        }

        Source = ForumUrls.Thread(Slug, ThreadId, Page).ToString();

        IsFavorite = (await cache.GetPositionAsync(ThreadId))?.IsFavorite ?? false;

        await cache.RememberPositionAsync(new ThreadReadState
        {
            ThreadId = ThreadId,
            Slug = Slug,
            Title = string.IsNullOrEmpty(ThreadTitle) ? Slug : ThreadTitle,
            Page = Page,
        });
    });

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [RelayCommand]
    private Task ToggleFavoriteAsync() => RunAsync(async _ =>
    {
        if (ThreadId <= 0)
        {
            return;
        }

        IsFavorite = await cache.ToggleFavoriteAsync(new ThreadReadState
        {
            ThreadId = ThreadId,
            Slug = Slug,
            Title = string.IsNullOrEmpty(ThreadTitle) ? Slug : ThreadTitle,
            Page = Page,
        });
    });

    // страницу мог сменить сам WebView — запоминаем, куда человека унесло
    public async Task RememberPageAsync(int page)
    {
        if (page == Page || page <= 0)
        {
            return;
        }

        Page = page;

        await cache.RememberPositionAsync(new ThreadReadState
        {
            ThreadId = ThreadId,
            Slug = Slug,
            Title = string.IsNullOrEmpty(ThreadTitle) ? Slug : ThreadTitle,
            Page = page,
        });
    }

    private static string Read(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) ? Uri.UnescapeDataString(value?.ToString() ?? string.Empty) : string.Empty;

    private static int ReadNumber(IDictionary<string, object> query, string key) =>
        int.TryParse(Read(query, key), out var number) ? number : 0;
}
