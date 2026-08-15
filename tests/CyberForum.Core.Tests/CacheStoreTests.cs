using CyberForum.Core.Storage;

namespace CyberForum.Core.Tests;

public class CacheStoreTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"cf-cache-{Guid.NewGuid():N}");
    private CacheStore _store = null!;

    public Task InitializeAsync()
    {
        _store = new CacheStore(_path) { FreshFor = TimeSpan.FromMinutes(10) };

        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        Directory.Delete(_path, recursive: true);
    }

    [Fact]
    public async Task Saved_page_comes_back()
    {
        var url = ForumUrls.Forum("python");

        await _store.SavePageAsync(url, "<html>тема</html>");

        Assert.Equal("<html>тема</html>", await _store.GetPageAsync(url));
    }

    [Fact]
    public async Task Missing_page_is_null()
    {
        Assert.Null(await _store.GetPageAsync(ForumUrls.Forum("csharp")));
    }

    [Fact]
    public async Task Stale_page_is_hidden_but_reachable_on_demand()
    {
        var url = ForumUrls.Thread("python", 42);
        await _store.SavePageAsync(url, "старое");

        // за нулевой срок свежим не считается ничего
        Assert.Null(await _store.GetPageAsync(url, TimeSpan.Zero));
        Assert.Equal("старое", await _store.GetPageAsync(url, TimeSpan.MaxValue));
    }

    [Fact]
    public async Task Second_save_replaces_the_first()
    {
        var url = ForumUrls.Forum("python", 3);

        await _store.SavePageAsync(url, "первое");
        await _store.SavePageAsync(url, "второе");

        Assert.Equal("второе", await _store.GetPageAsync(url));
    }

    [Fact]
    public async Task Reading_position_survives()
    {
        await _store.RememberPositionAsync(new ThreadReadState
        {
            ThreadId = 3212200,
            Slug = "python",
            Title = "База без SQL",
            Page = 2,
            LastPostId = 17648021,
            IsFavorite = true,
        });

        var state = await _store.GetPositionAsync(3212200);

        Assert.NotNull(state);
        Assert.Equal(2, state!.Page);
        Assert.Equal(17648021, state.LastPostId);
        Assert.True(state.IsFavorite);

        var favorites = await _store.GetFavoritesAsync();
        Assert.Single(favorites);
    }

    [Fact]
    public async Task Закладку_можно_поставить_и_снять()
    {
        var thread = new ThreadReadState
        {
            ThreadId = 3225075,
            Slug = "1c-bitrix",
            Title = "Не открывается sidepanel",
        };

        // темы ещё нет в истории — запись должна завестись сама
        Assert.True(await _store.ToggleFavoriteAsync(thread));
        Assert.Single(await _store.GetFavoritesAsync());

        Assert.False(await _store.ToggleFavoriteAsync(thread));
        Assert.Empty(await _store.GetFavoritesAsync());
    }

    [Fact]
    public async Task Заход_в_тему_не_сбрасывает_закладку()
    {
        var thread = new ThreadReadState
        {
            ThreadId = 3225071,
            Slug = "1c-admin",
            Title = "Ошибка после переустановки",
        };

        await _store.ToggleFavoriteAsync(thread);

        await _store.RememberPositionAsync(new ThreadReadState
        {
            ThreadId = 3225071,
            Slug = "1c-admin",
            Title = "Ошибка после переустановки",
            Page = 4,
        });

        var state = await _store.GetPositionAsync(3225071);

        Assert.NotNull(state);
        Assert.True(state!.IsFavorite);
        Assert.Equal(4, state.Page);
    }

    [Fact]
    public async Task Число_прочитанных_ответов_не_затирается_повторным_заходом()
    {
        // из списка тем: знаем, сколько ответов было на момент открытия
        await _store.RememberPositionAsync(new ThreadReadState
        {
            ThreadId = 3212200,
            Slug = "python",
            Title = "База без SQL",
            Replies = 74,
        });

        // а страница темы про это не знает и сохраняет только позицию
        await _store.RememberPositionAsync(new ThreadReadState
        {
            ThreadId = 3212200,
            Slug = "python",
            Title = "База без SQL",
            Page = 3,
        });

        var state = await _store.GetPositionAsync(3212200);

        Assert.NotNull(state);
        Assert.Equal(74, state!.Replies);
        Assert.Equal(3, state.Page);
    }

    [Fact]
    public async Task Old_pages_can_be_swept()
    {
        await _store.SavePageAsync(ForumUrls.Forum("python"), "тело");

        Assert.Equal(0, await _store.ClearOlderThanAsync(TimeSpan.FromHours(1)));
        Assert.Equal(1, await _store.ClearOlderThanAsync(TimeSpan.Zero));
        Assert.Null(await _store.GetPageAsync(ForumUrls.Forum("python"), TimeSpan.MaxValue));
    }
}
