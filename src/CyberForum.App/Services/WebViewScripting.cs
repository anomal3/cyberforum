namespace CyberForum.App.Services;

/// <summary>
/// Выполняет скрипт в WebView напрямую через платформенный объект.
/// Штатный EvaluateJavaScriptAsync у MAUI по дороге экранирует код, и до страницы
/// он доезжает уже поломанным — наша читалка так просто не запускалась.
/// </summary>
public static class WebViewScripting
{
    public static void Run(WebView view, string script, Action<string?>? report = null)
    {
        // WebView на Android принимает скрипты только с главного потока и молча
        // проглатывает вызовы с любого другого — отсюда и «читалка не работает»
        if (!MainThread.IsMainThread)
        {
            MainThread.BeginInvokeOnMainThread(() => Run(view, script, report));
            return;
        }

        var platform = view.Handler?.PlatformView;

        if (platform is null)
        {
            report?.Invoke("webview ещё не создан");
            return;
        }

#if ANDROID
        if (platform is Android.Webkit.WebView native)
        {
            native.Settings.JavaScriptEnabled = true;
            native.EvaluateJavascript(script, report is null ? null : new ResultCallback(report));
        }
#elif IOS || MACCATALYST
        if (platform is WebKit.WKWebView native)
        {
            native.EvaluateJavaScript(new Foundation.NSString(script), (result, error) =>
                report?.Invoke(error is null ? result?.ToString() : error.LocalizedDescription));
        }
#endif
    }

    /// <summary>
    /// То же самое, но с ожиданием ответа. Удобно, когда со страницы надо что-то
    /// забрать, а не просто её причесать.
    /// </summary>
    public static Task<string?> AskAsync(WebView view, string script)
    {
        var answer = new TaskCompletionSource<string?>();

        Run(view, script, result => answer.TrySetResult(result));

        // страница может и не ответить — ждать её вечно мы не будем
        return Task.WhenAny(answer.Task, Task.Delay(4000).ContinueWith(_ => (string?)null))
            .Unwrap();
    }

#if ANDROID
    // Android отдаёт результат скрипта только через колбэк
    private sealed class ResultCallback(Action<string?> report) : Java.Lang.Object, Android.Webkit.IValueCallback
    {
        public void OnReceiveValue(Java.Lang.Object? value) => report(value?.ToString());
    }
#endif
}
