using Android.Webkit;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using AWebView = Android.Webkit.WebView;

namespace CyberForum.App.Platforms.Android;

/// <summary>
/// Пускает в страницу только то, что нам нужно для чтения. Без этого чужие
/// вставки успевают перерисовать документ под себя, и причёсывать уже нечего.
/// </summary>
internal sealed class RequestFilterWebViewClient : MauiWebViewClient
{
    // свои домены: сам форум и его статика
    private static readonly string[] Ours =
    {
        "cyberforum.ru",
        "cyberstatic.net",
    };

    private static readonly string[] ImageTypes =
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".svg", ".ico",
    };

    public RequestFilterWebViewClient(WebViewHandler handler)
        : base(handler)
    {
    }

    public override WebResourceResponse? ShouldInterceptRequest(AWebView? view, IWebResourceRequest? request)
    {
        var url = request?.Url;

        // саму страницу трогать нельзя, иначе тема не откроется вообще
        if (url is null || request!.IsForMainFrame)
        {
            return base.ShouldInterceptRequest(view, request);
        }

        var host = url.Host ?? string.Empty;
        var address = url.ToString() ?? string.Empty;

        if (IsOurs(host))
        {
            // Стили и скрипты форума не нужны совсем: его вёрстку мы всё равно
            // выбрасываем целиком и рисуем свою. А весят они прилично, и пока
            // грузятся — тема не открывается.
            return IsStyleOrScript(address)
                ? Blocked()
                : base.ShouldInterceptRequest(view, request);
        }

        // картинки в сообщениях люди заливают куда попало — их пропускаем,
        // а чужие скрипты и фреймы для чтения не нужны вовсе
        if (LooksLikeImage(request, url.ToString() ?? string.Empty))
        {
            return base.ShouldInterceptRequest(view, request);
        }

        return Blocked();
    }

    private static bool IsStyleOrScript(string address)
    {
        var path = address;
        var cut = path.IndexOf('?');

        if (cut > 0)
        {
            path = path.Substring(0, cut);
        }

        return path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
               path.EndsWith(".js", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsOurs(string host)
    {
        foreach (var own in Ours)
        {
            if (host.Equals(own, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + own, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool LooksLikeImage(IWebResourceRequest request, string url)
    {
        var headers = request.RequestHeaders;

        if (headers is not null &&
            headers.TryGetValue("Accept", out var accept) &&
            accept is not null &&
            accept.Contains("image/", StringComparison.OrdinalIgnoreCase) &&
            !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var path = url;
        var cut = path.IndexOf('?');

        if (cut > 0)
        {
            path = path.Substring(0, cut);
        }

        foreach (var type in ImageTypes)
        {
            if (path.EndsWith(type, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static WebResourceResponse Blocked() =>
        new("text/plain", "utf-8", 200, "OK", new Dictionary<string, string>(), new MemoryStream());
}
