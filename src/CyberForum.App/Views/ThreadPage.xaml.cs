using CyberForum.App.Services;
using CyberForum.App.ViewModels;
using CyberForum.Core;
using CyberForum.Core.Posting;

namespace CyberForum.App.Views;

public partial class ThreadPage : ContentPage
{
    private readonly ThreadViewModel _viewModel;
    private readonly ThreadReaderScript _reader;
    private readonly DownloadService _downloads;
    private readonly PostingService _posting;
    private readonly ReplyContext _reply;
    private readonly SessionService _session;

    private const string FontKey = "reader.font";

    private IDispatcherTimer? _timer;
    private int _attempts;
    private bool _ready;
    private bool _sheetOpen;
    private bool _formAsked;

    public ThreadPage(
        ThreadViewModel viewModel,
        ThreadReaderScript reader,
        DownloadService downloads,
        PostingService posting,
        ReplyContext reply,
        SessionService session)
    {
        InitializeComponent();

        BindingContext = _viewModel = viewModel;
        _reader = reader;
        _downloads = downloads;
        _posting = posting;
        _reply = reply;
        _session = session;

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

        _reply.Reset(_viewModel.ThreadId, _viewModel.ThreadTitle);

        // вернулись из полного редактора, а он уже всё отправил — перечитываем тему
        if (_reply.Posted)
        {
            _reply.Posted = false;
            _reply.Clear();
            Posts.Reload();
        }

        Sheet.Text = _reply.Draft;
        ShowQuoteMark();

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
        _formAsked = false;

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
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                HideCurtain();
                ApplyFont();
                await TakeFormAsync();

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

    /* Читалка снимает форму быстрого ответа со страницы до того, как перебрать её
       в свою вёрстку, — там лежат одноразовые ключи. Есть форма, значит форум нас
       узнал и отвечать в этой теме можно. */
    private async Task TakeFormAsync()
    {
        var answer = await WebViewScripting.AskAsync(Posts, "JSON.stringify(window.cfForm||null)");
        var form = ReplyContext.FromPage(answer);

        if (form is not null)
        {
            _reply.Form = form;
        }
        else if (_reply.Form is null && !_formAsked && _session.Current.IsAuthenticated)
        {
            // за формой ходим один раз на открытие темы, а не каждый круг сторожа
            _formAsked = true;

            /* Страница могла и не отдать форму: WebView иногда перечитывает её сам,
               и наш слепок пропадает. Тогда просим форму у форума напрямую —
               лишний запрос дешевле, чем пропавшая кнопка ответа. */
            try
            {
                _reply.Form = await _posting.GetFormAsync(_viewModel.ThreadId);
            }
            catch (Exception)
            {
                // не вышло — значит в этой теме отвечать нельзя, и кнопки не будет
            }
        }

        ReplyBar.IsVisible = _reply.Form is not null && !_sheetOpen;

        // полоска ответа висит поверх страницы — освобождаем под неё место снизу
        if (_reply.Form is not null)
        {
            WebViewScripting.Run(Posts,
                "(function(){if(document.body){document.body.style.paddingBottom='84px';}return 'место есть';})()");
        }
    }

    private void OnReplyBarTapped(object? sender, TappedEventArgs e) => OpenSheet();

    private void OpenSheet()
    {
        _sheetOpen = true;

        ReplyBar.IsVisible = false;
        Sheet.Open();
    }

    private void CloseSheet() => Sheet.Close();

    // шторка закрылась сама — прибираем за ней
    private void OnSheetClosed(object? sender, EventArgs e)
    {
        _sheetOpen = false;
        _reply.Draft = Sheet.Text;

        ReplyBar.IsVisible = _reply.Form is not null;
    }

    // жёсткая кнопка «назад» сперва закрывает шторку, а не уводит из темы
    protected override bool OnBackButtonPressed()
    {
        if (!_sheetOpen)
        {
            return base.OnBackButtonPressed();
        }

        CloseSheet();
        return true;
    }

    private void ShowQuoteMark() =>
        Sheet.ShowQuote(_reply.QuotePostId is null ? null : _reply.QuoteAuthor ?? string.Empty);

    private void OnQuoteDropped(object? sender, EventArgs e)
    {
        _reply.QuotePostId = null;
        _reply.QuoteAuthor = null;
    }

    /* Кнопки под сообщением зовут нас сменой адреса: cfact:вид:номер:автор.
       Своего моста между страницей и приложением у WebView нет, а этот работает
       одинаково на всех платформах. */
    private async Task HandleActionAsync(string request)
    {
        var parts = request.Split(':');

        if (parts.Length < 2 || !int.TryParse(parts[1], out var postId))
        {
            return;
        }

        var kind = parts[0];
        var author = parts.Length > 2 ? Uri.UnescapeDataString(parts[2]) : string.Empty;

        switch (kind)
        {
            case "quote":
                await StartQuoteAsync(postId, author);
                break;

            case "thank":
                await ThankAsync(postId);
                break;

            case "best":
                await MarkBestAsync(postId);
                break;
        }
    }

    /// <summary>
    /// Цитату собирает сам форум: просим у него форму ответа на это сообщение и
    /// берём оттуда готовый текст. Своими руками bb-код цитаты не сложить — исходника
    /// сообщения у нас нет, только показанная разметка.
    /// </summary>
    private async Task StartQuoteAsync(int postId, string author)
    {
        _reply.QuotePostId = postId;
        _reply.QuoteAuthor = author;

        ShowQuoteMark();
        OpenSheet();

        if (!string.IsNullOrWhiteSpace(Sheet.Text))
        {
            return;
        }

        try
        {
            var form = await _posting.GetFormAsync(_viewModel.ThreadId, postId);

            if (form is not null)
            {
                _reply.Form = form;

                if (!string.IsNullOrWhiteSpace(form.Message))
                {
                    Sheet.Text = form.Message + "\n\n";
                }
            }
        }
        catch (Exception)
        {
            // не дал форму — не беда, ответим без цитаты, текст человек напишет сам
            Say("Цитату форум не отдал, но ответить можно и так.");
        }
    }

    private async Task ThankAsync(int postId)
    {
        var token = _session.Current.SecurityToken;

        if (string.IsNullOrEmpty(token))
        {
            Say("Сказать спасибо можно только войдя на форум.");
            return;
        }

        try
        {
            var result = await _posting.ThankAsync(postId, token);

            Say(result.Ok ? "Спасибо отправлено" : result.Message ?? "Форум не принял спасибо.");

            if (result.Ok)
            {
                MarkButtonDone(postId, "thank");
            }
        }
        catch (Exception error)
        {
            Say("Не вышло сказать спасибо: " + error.Message);
        }
    }

    private async Task MarkBestAsync(int postId)
    {
        var token = _session.Current.SecurityToken;

        if (string.IsNullOrEmpty(token))
        {
            Say("Отмечать лучший ответ может только автор темы.");
            return;
        }

        try
        {
            var result = await _posting.MarkAnswerAsync(_viewModel.ThreadId, postId, token);

            Say(result.Ok ? "Отмечено как лучший ответ" : result.Message ?? "Форум не дал отметить ответ.");

            if (result.Ok)
            {
                MarkButtonDone(postId, "best");
            }
        }
        catch (Exception error)
        {
            Say("Не вышло отметить ответ: " + error.Message);
        }
    }

    // страницу заново не перечитываем: достаточно погасить кнопку, по которой нажали
    private void MarkButtonDone(int postId, string kind) =>
        WebViewScripting.Run(Posts,
            $"(function(){{var p=document.getElementById('post-{postId}');" +
            $"if(!p){{return 'нет сообщения';}}var b=p.querySelector('[data-kind=\"{kind}\"]');" +
            "if(b){b.classList.add('done');}return 'отмечено';})()");

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var text = Sheet.Text.Trim();

        if (text.Length == 0)
        {
            Say("Пустое сообщение отправлять некуда.");
            return;
        }

        SetSending(true);

        try
        {
            var form = _reply.Form ?? await _posting.GetFormAsync(_viewModel.ThreadId);

            if (form is null)
            {
                Say("Форум не дал формы ответа. Попробуй войти заново.");
                return;
            }

            var result = await _posting.SendAsync(form, text);

            if (!result.Ok)
            {
                Say(result.Message ?? "Форум ответ не принял.");
                return;
            }

            Sheet.Text = string.Empty;
            _reply.Clear();

            CloseSheet();
            ShowQuoteMark();

            /* Ключи в форме одноразовые: следующий ответ придётся собирать заново,
               и заодно тема покажет только что отправленное сообщение. */
            _reply.Form = null;
            Posts.Reload();
        }
        catch (Exception error)
        {
            Say("Не получилось отправить: " + error.Message);
        }
        finally
        {
            SetSending(false);
        }
    }

    private void SetSending(bool going)
    {
        Sheet.SetBusy(going);
    }

    private void Say(string message)
    {
        Sheet.Say(message);

        if (!_sheetOpen)
        {
            _viewModel.ErrorMessage = message;
        }
    }

    private async void OnFullEditorClicked(object? sender, EventArgs e)
    {
        _reply.Draft = Sheet.Text;
        _reply.Form ??= await _posting.GetFormAsync(_viewModel.ThreadId);

        CloseSheet();

        await Shell.Current.GoToAsync(Routes.ToCompose());
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

        // нажали кнопку под сообщением: ответить, сказать спасибо, отметить ответ
        if (e.Url.StartsWith("cfact:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            await HandleActionAsync(e.Url["cfact:".Length..]);
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
