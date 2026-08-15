using CyberForum.App.Services;
using CyberForum.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace CyberForum.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // колокольчик держим спрятанным, пока форум не скажет, что есть новости
        if (IPlatformApplication.Current?.Services.GetService<CabinetService>() is { } cabinet)
        {
            cabinet.NotificationsChanged += (_, count) =>
                MainThread.BeginInvokeOnMainThread(() => ShowBell(count));
        }

        // страницы вглубь: категория -> раздел -> тема
        Routing.RegisterRoute(Routes.Category, typeof(CategoryPage));
        Routing.RegisterRoute(Routes.Favorites, typeof(FavoritesPage));
        Routing.RegisterRoute(Routes.Links, typeof(LinkListPage));
        Routing.RegisterRoute(Routes.Reputation, typeof(ReputationPage));
        Routing.RegisterRoute(Routes.Blog, typeof(BlogPage));
        Routing.RegisterRoute(Routes.BlogEntry, typeof(BlogEntryPage));
        Routing.RegisterRoute(Routes.Web, typeof(WebPage));
        Routing.RegisterRoute(Routes.Image, typeof(ImageViewerPage));
        Routing.RegisterRoute(Routes.ThreadList, typeof(ThreadListPage));
        Routing.RegisterRoute(Routes.Thread, typeof(ThreadPage));
        Routing.RegisterRoute(Routes.Login, typeof(LoginPage));
    }

    /// <summary>
    /// Своих значков-кружочков на вкладках MAUI не умеет, поэтому число ставим
    /// прямо в подпись, а колокольчику подменяем картинку на такую же, но с точкой.
    /// </summary>
    private void ShowBell(int count)
    {
        BellTab.IsVisible = count > 0;

#if ANDROID
        // панель перестраивается не мгновенно — вешаем значок следующим кадром
        Dispatcher.Dispatch(() => Platforms.Android.TabBadge.Show(3, count));
#endif
    }
}

public static class Routes
{
    public const string Category = "category";
    public const string Favorites = "favorites";
    public const string Links = "links";
    public const string Reputation = "reputation";

    public const string Blog = "blog";
    public const string BlogEntry = "blogentry";
    public const string Web = "web";
    public const string Image = "image";

    // один экран на три списка: недавно читали, отмеченные ответы, закладки
    public static string ToLinks(string kind) => $"{Links}?kind={kind}";

    public static string ToBlog(int userId) => $"{Blog}?user={userId}";

    public static string ToBlogEntry(int userId, int entryId, string title) =>
        $"{BlogEntry}?user={userId}&entry={entryId}&title={Uri.EscapeDataString(title)}";

    public static string ToImage(string url) => $"{Image}?url={Uri.EscapeDataString(url)}";

    // страница форума внутри приложения — для того, что мы не переписываем
    public static string ToWeb(Uri url, string title) =>
        $"{Web}?url={Uri.EscapeDataString(url.ToString())}&title={Uri.EscapeDataString(title)}";
    public const string ThreadList = "threads";
    public const string Thread = "thread";
    public const string Login = "login";

    public static string ToCategory(string slug, string title) =>
        $"{Category}?slug={Uri.EscapeDataString(slug)}&title={Uri.EscapeDataString(title)}";

    public static string ToThreadList(string slug, string title) =>
        $"{ThreadList}?slug={Uri.EscapeDataString(slug)}&title={Uri.EscapeDataString(title)}";

    public static string ToThread(string slug, int threadId, string title, int page = 1) =>
        $"{Thread}?slug={Uri.EscapeDataString(slug)}&id={threadId}&page={page}&title={Uri.EscapeDataString(title)}";
}
