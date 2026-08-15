using CyberForum.App.Services;
using CyberForum.App.ViewModels;
using CyberForum.Core;

namespace CyberForum.App.Views;

public partial class ThreadPage : ContentPage
{
    private readonly ThreadViewModel _viewModel;
    private readonly ThreadReaderScript _reader;
    private readonly DownloadService _downloads;

    private const string FontKey = "reader.font";

    private IDispatcherTimer? _timer;
    private int _attempts;
    private bool _ready;

    public ThreadPage(ThreadViewModel viewModel, ThreadReaderScript reader, DownloadService downloads)
    {
        InitializeComponent();

        BindingContext = _viewModel = viewModel;
        _reader = reader;
        _downloads = downloads;

        Posts.HandlerChanged += (_, _) => PrepareWebView();
    }

    /// <summary>
    /// Лишние запросы режем на подлёте: иначе чужие вставки перерисовывают
    /// страницу и причёсывать становится нечего. Заодно делаем сам webview
    /// прозрачным, чтобы сквозь него была видна заставка. Только в этом окне:
    /// на странице входа без чужих скриптов reCAPTCHA не пройти.
    /// </summary>
    private void PrepareWebView()
    {
#if ANDROID
        if (Posts.Handler is Microsoft.Maui.Handlers.WebViewHandler handler)
        {
            if (AppFeatures.FilterRequests)
            {
                handler.PlatformView.SetWebViewClient(
                    new Platforms.Android.RequestFilterWebViewClient(handler));
            }

            handler.PlatformView.SetBackgroundColor(global::Android.Graphics.Color.Transparent);
        }
#endif
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        ShowCurtain();

        await _reader.PrepareAsync();
        await _viewModel.LoadCommand.ExecuteAsync(null);

        StartTidying();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopTidying();
    }

    /// <summary>
    /// Причёсываем страницу часто и подряд, а не один раз по событию загрузки:
    /// сообщения появляются в разметке задолго до того, как страница «догрузилась»,
    /// и ждать, пока догрузится всё остальное, незачем. Первый заход прячет страницу
    /// за нашей заставкой, чтобы сырая вёрстка вообще не мелькала.
    /// </summary>
    private void StartTidying()
    {
        StopTidying();

        _attempts = 0;
        _ready = false;

        ShowCurtain();

        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(120);
        _timer.Tick += (_, _) => Tidy();
        _timer.Start();

        Tidy();
    }

    private void StopTidying()
    {
        _timer?.Stop();
        _timer = null;
    }

    // Бегунок катается по дорожке туда-сюда, пока страница не готова.
    private void ShowCurtain()
    {
        Curtain.IsVisible = true;
        Curtain.Opacity = 1;
        Runner.TranslationX = 0;

        this.AbortAnimation("runner");

        var run = new Animation(value => Runner.TranslationX = value, 0, 126, Easing.SinInOut);

        run.Commit(this, "runner", 16, 850, null, (_, _) => Runner.TranslationX = 0, () => !_ready);
    }

    private async void HideCurtain()
    {
        if (!Curtain.IsVisible)
        {
            return;
        }

        this.AbortAnimation("runner");

        await Curtain.FadeToAsync(0, 180, Easing.CubicOut);
        Curtain.IsVisible = false;
    }

    private void Tidy()
    {
        _attempts++;

        // секунд двадцать ждём сообщений, дальше сдаёмся и показываем что есть
        if (!_ready && _attempts > 160)
        {
            StopTidying();
            Unhide();
            HideCurtain();
            _viewModel.ErrorMessage = "Тему показываем как есть: причесать не вышло";
            return;
        }

        if (_reader.Script is { } script)
        {
            WebViewScripting.Run(Posts, script, OnTidyResult);
        }
    }

    private void OnTidyResult(string? result)
    {
        var text = result?.Trim('"') ?? string.Empty;

        if (text.StartsWith("постов", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("уже причёсано", StringComparison.OrdinalIgnoreCase))
        {
            _ready = true;

            // дальше просто присматриваем: страницу могут перерисовать,
            // и тогда её надо причесать заново
            MainThread.BeginInvokeOnMainThread(() =>
            {
                HideCurtain();
                ApplyFont();

                if (_timer is not null)
                {
                    _timer.Interval = TimeSpan.FromSeconds(2);
                }
            });

            return;
        }

        /* На ошибку не сдаёмся: половина из них — это заход на полупустой документ,
           который через полсекунды будет уже нормальным. Попытки и так ограничены. */
    }

    // Читают люди по-разному, поэтому размер букв ходит по кругу и запоминается.
    private static readonly int[] Sizes = [16, 18, 20, 22, 14];

    private void OnFontClicked(object? sender, EventArgs e)
    {
        var step = (Preferences.Get(FontKey, 0) + 1) % Sizes.Length;

        Preferences.Set(FontKey, step);
        ApplyFont();
    }

    private void ApplyFont()
    {
        var size = Sizes[Math.Clamp(Preferences.Get(FontKey, 0), 0, Sizes.Length - 1)];

        // размер вешаем на body: в стилях у него свой font, и он перебил бы html
        WebViewScripting.Run(Posts,
            $"(function(){{if(document.body){{document.body.style.fontSize='{size}px';}}return 'шрифт {size}';}})()");
    }

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_viewModel.Source))
        {
            return;
        }

        await Share.RequestAsync(new ShareTextRequest
        {
            Uri = _viewModel.Source,
            Title = _viewModel.ThreadTitle,
            Subject = _viewModel.ThreadTitle,
        });
    }

    private static Task ShowImageAsync(string encoded) =>
        Shell.Current.GoToAsync(Routes.ToImage(Uri.UnescapeDataString(encoded)));

    // Вложение форум отдаёт только по куке, поэтому качаем сами и говорим, как прошло.
    private async Task SaveFileAsync(string encoded)
    {
        var url = Uri.UnescapeDataString(encoded);

        try
        {
            _viewModel.ErrorMessage = "Скачиваем файл…";

            var name = await _downloads.SaveAsync(url);

            _viewModel.ErrorMessage = name is null
                ? "Файл не скачался. Возможно, форум отдаёт его только вошедшим."
                : $"Сохранено в «Загрузки»: {name}";
        }
        catch (Exception error)
        {
            _viewModel.ErrorMessage = "Не получилось скачать файл: " + error.Message;
        }
    }

    // Когда причесать не вышло, снимаем свою же ширму — пусть хоть так, чем ничего.
    private void Unhide() =>
        WebViewScripting.Run(Posts, "(function(){var s=document.getElementById('cf-hide');if(s){s.remove();}return 'показали';})()");

    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        if (e.Result != WebNavigationResult.Success)
        {
            // заставку снимаем, иначе человек будет смотреть на бегунок до посинения
            StopTidying();
            HideCurtain();

            _viewModel.ErrorMessage = "Не получилось открыть тему. Проверь связь и попробуй ещё раз.";
            return;
        }

        var location = ForumUrls.Parse(e.Url);

        if (location.Kind == ForumUrlKind.Thread)
        {
            await _viewModel.RememberPageAsync(location.Page);
        }

        // страница сменилась — причёсываем заново
        StartTidying();
    }

    // Внутри форума ходим прямо в этом же WebView, наружу — во внешний браузер.
    private async void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // читалка так зовёт нас на помощь: картинку показать, файл скачать
        if (e.Url.StartsWith("cfimage:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            await ShowImageAsync(e.Url["cfimage:".Length..]);
            return;
        }

        if (e.Url.StartsWith("cffile:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            await SaveFileAsync(e.Url["cffile:".Length..]);
            return;
        }

        if (e.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            e.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri))
        {
            return;
        }

        if (ForumUrls.IsForumHost(uri) || uri.Host.EndsWith(ForumUrls.StaticHost, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        e.Cancel = true;

        if (!await Launcher.OpenAsync(uri))
        {
            _viewModel.ErrorMessage = "Не нашлось, чем открыть эту ссылку.";
        }
    }
}
