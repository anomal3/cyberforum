using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.App.Services;
using CyberForum.Core;
using CyberForum.Core.Models;
using CyberForum.Core.Storage;

namespace CyberForum.App.ViewModels;

/// <summary>
/// Профиль: кто мы, короткие счётчики и ходы во все списки. Тяжёлое (кабинет форума
/// и страницу участника) тянем только для вошедших — гостю там нечего показывать.
/// </summary>
public sealed partial class ProfileViewModel(
    SessionService session,
    CabinetService cabinet,
    CacheStore cache) : BaseViewModel
{
    public ObservableCollection<ProfileField> About { get; } = [];

    public ObservableCollection<ProfileField> Stats { get; } = [];

    public ObservableCollection<BlogEntry> Blog { get; } = [];

    [ObservableProperty]
    public partial string Status { get; set; } = "Читаем гостем";

    [ObservableProperty]
    public partial bool IsSignedIn { get; set; }

    [ObservableProperty]
    public partial string? AvatarUrl { get; set; }

    [ObservableProperty]
    public partial int UserId { get; set; }

    [ObservableProperty]
    public partial string FavoritesCount { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HistoryCount { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BestAnswersCount { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BookmarksCount { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ReputationCount { get; set; } = string.Empty;

    public bool HasAbout => About.Count > 0;

    public bool HasStats => Stats.Count > 0;

    public bool HasBlog => Blog.Count > 0;

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async token =>
    {
        Title = "Профиль";

        var state = await session.RefreshAsync();
        Apply(state.IsAuthenticated, state.UserName);

        FavoritesCount = Count((await cache.GetFavoritesAsync()).Count);
        HistoryCount = Count((await cache.GetHistoryAsync(100)).Count);

        if (!state.IsAuthenticated)
        {
            Clear();
            return;
        }

        var current = await cabinet.RefreshAsync(token);

        BestAnswersCount = Count(current.BestAnswersTotal > 0 ? current.BestAnswersTotal : current.BestAnswers.Count);
        BookmarksCount = Count(current.Bookmarks.Count);
        ReputationCount = current.ReputationTotal > 0 ? $"{current.ReputationTotal} баллов" : string.Empty;

        if (current.UserId is { } id and > 0)
        {
            UserId = id;
            await FillProfileAsync(id, token);
        }
    });

    private async Task FillProfileAsync(int userId, CancellationToken token)
    {
        var profile = await cabinet.MemberAsync(userId, token);

        AvatarUrl = profile.AvatarUrl;

        Fill(Stats, profile.Stats);
        Fill(About, profile.About);

        Blog.Clear();
        foreach (var entry in profile.Blog.Take(5))
        {
            Blog.Add(entry);
        }

        OnPropertyChanged(nameof(HasAbout));
        OnPropertyChanged(nameof(HasStats));
        OnPropertyChanged(nameof(HasBlog));
    }

    [RelayCommand]
    private static Task OpenFavoritesAsync() => Shell.Current.GoToAsync(Routes.Favorites);

    [RelayCommand]
    private static Task OpenHistoryAsync() => Shell.Current.GoToAsync(Routes.ToLinks("history"));

    // своих тем у форума отдельным списком нет — ищем их поиском по автору
    [RelayCommand]
    private static Task OpenMyThreadsAsync() => Shell.Current.GoToAsync(Routes.MyThreads);

    [RelayCommand]
    private static Task OpenBestAnswersAsync() => Shell.Current.GoToAsync(Routes.ToLinks("best"));

    [RelayCommand]
    private static Task OpenBookmarksAsync() => Shell.Current.GoToAsync(Routes.ToLinks("bookmarks"));

    [RelayCommand]
    private static Task OpenReputationAsync() => Shell.Current.GoToAsync(Routes.Reputation);

    // блоги форум отдаёт обычным запросом, поэтому читаем их у себя, а не в браузере
    [RelayCommand]
    private Task OpenBlogAsync(BlogEntry? entry)
    {
        if (entry is null || UserId <= 0)
        {
            return Task.CompletedTask;
        }

        var id = EntryId(entry.Url);

        return id > 0
            ? Shell.Current.GoToAsync(Routes.ToBlogEntry(UserId, id, entry.Title))
            : Shell.Current.GoToAsync(Routes.ToBlog(UserId));
    }

    [RelayCommand]
    private Task OpenBlogListAsync() =>
        UserId > 0 ? Shell.Current.GoToAsync(Routes.ToBlog(UserId)) : Task.CompletedTask;

    // Правка профиля и записей — это формы форума со своими токенами и вложениями.
    // Переписывать их у себя нечестно и долго, поэтому показываем страницу форума
    // внутри приложения: человек правит там же, не уходя в браузер.
    [RelayCommand]
    private static Task EditAboutAsync() =>
        Shell.Current.GoToAsync(Routes.ToWeb(ForumUrls.EditProfile(), "Правка профиля"));

    [RelayCommand]
    private static Task EditBlogAsync() =>
        Shell.Current.GoToAsync(Routes.ToWeb(ForumUrls.BlogCabinet(), "Кабинет блога"));

    private static int EntryId(string url)
    {
        var match = System.Text.RegularExpressions.Regex.Match(url, @"/blogs/\d+/(?:blog)?(?<id>\d+)");

        return match.Success && int.TryParse(match.Groups["id"].Value, out var id) ? id : 0;
    }

    [RelayCommand]
    private static Task SignInAsync() => Shell.Current.GoToAsync(Routes.Login);

    [RelayCommand]
    private Task SignOutAsync() => RunAsync(async _ =>
    {
        await session.ForgetAsync();
        cabinet.Forget();

        Apply(false, null);
        Clear();
    });

    private static string Count(int value) => value > 0 ? value.ToString() : string.Empty;

    private static void Fill(ObservableCollection<ProfileField> target, IEnumerable<ProfileField> source)
    {
        target.Clear();

        foreach (var field in source)
        {
            target.Add(field);
        }
    }

    private void Clear()
    {
        About.Clear();
        Stats.Clear();
        Blog.Clear();

        AvatarUrl = null;
        BestAnswersCount = string.Empty;
        BookmarksCount = string.Empty;
        ReputationCount = string.Empty;

        OnPropertyChanged(nameof(HasAbout));
        OnPropertyChanged(nameof(HasStats));
        OnPropertyChanged(nameof(HasBlog));
    }

    private void Apply(bool signedIn, string? name)
    {
        IsSignedIn = signedIn;
        Status = signedIn ? name ?? "Вошли" : "Читаем гостем";
    }
}
