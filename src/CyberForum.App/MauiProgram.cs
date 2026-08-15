using CyberForum.App.Services;
using CyberForum.App.ViewModels;
using CyberForum.App.Views;
using CyberForum.Core;
using CyberForum.Core.Http;
using CyberForum.Core.Parsing;
using CyberForum.Core.Posting;
using CyberForum.Core.Rendering;
using CyberForum.Core.Storage;
using Microsoft.Extensions.Logging;

namespace CyberForum.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Клиент один на всё приложение: у него общие куки и общий тормоз на частоту запросов
        builder.Services.AddSingleton<ForumHttpClient>();
        builder.Services.AddSingleton(_ =>
            new CacheStore(Path.Combine(FileSystem.CacheDirectory, "forum")));
        builder.Services.AddSingleton(services =>
            new ForumClient(services.GetRequiredService<ForumHttpClient>(), services.GetRequiredService<CacheStore>()));
        builder.Services.AddSingleton<ThreadReaderScript>();

        builder.Services.AddSingleton<CookieHarvester>();
        builder.Services.AddSingleton<SessionService>();
        builder.Services.AddSingleton<SearchService>();
        builder.Services.AddSingleton<CabinetService>();
        builder.Services.AddSingleton<PostContentSanitizer>();
        builder.Services.AddSingleton<BlogDocumentBuilder>();
        builder.Services.AddSingleton<DownloadService>();
        builder.Services.AddSingleton<PostingService>();
        builder.Services.AddSingleton<ReplyContext>();

        builder.Services.AddSingleton<FeedViewModel>();
        builder.Services.AddSingleton<ForumsViewModel>();
        builder.Services.AddTransient<CategoryViewModel>();
        builder.Services.AddTransient<FavoritesViewModel>();
        builder.Services.AddTransient<LinkListViewModel>();
        builder.Services.AddTransient<ReputationViewModel>();
        builder.Services.AddTransient<NoticesViewModel>();
        builder.Services.AddTransient<BlogViewModel>();
        builder.Services.AddTransient<BlogEntryViewModel>();
        builder.Services.AddTransient<ThreadListViewModel>();
        builder.Services.AddTransient<ThreadViewModel>();
        builder.Services.AddTransient<ProfileViewModel>();
        builder.Services.AddTransient<MyThreadsViewModel>();
        builder.Services.AddSingleton<SearchViewModel>();

        builder.Services.AddSingleton<FeedPage>();
        builder.Services.AddSingleton<ForumsPage>();
        builder.Services.AddTransient<CategoryPage>();
        builder.Services.AddTransient<FavoritesPage>();
        builder.Services.AddTransient<LinkListPage>();
        builder.Services.AddTransient<ReputationPage>();
        builder.Services.AddTransient<NoticesPage>();
        builder.Services.AddTransient<BlogPage>();
        builder.Services.AddTransient<BlogEntryPage>();
        builder.Services.AddTransient<WebPage>();
        builder.Services.AddTransient<ImageViewerPage>();
        builder.Services.AddTransient<ThreadListPage>();
        builder.Services.AddTransient<ThreadPage>();
        builder.Services.AddTransient<ComposePage>();
        builder.Services.AddTransient<MyThreadsPage>();
        builder.Services.AddTransient<ProfilePage>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddSingleton<SearchPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

