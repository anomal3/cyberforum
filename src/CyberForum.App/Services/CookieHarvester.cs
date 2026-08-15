using System.Net;
using CyberForum.Core;

namespace CyberForum.App.Services;

/// <summary>
/// Достаёт куки форума из системного WebView. Без этого никак: вход закрыт reCAPTCHA,
/// человек проходит её сам в WebView, а нам остаётся подобрать выданную сессию.
/// </summary>
public sealed partial class CookieHarvester
{
    public partial Task<IReadOnlyList<Cookie>> CollectAsync();

    /// <summary>
    /// Обратная дорога: отдаём куки в WebView. Нужна после входа своей формой —
    /// иначе тему он покажет глазами гостя и без наших прав.
    /// </summary>
    public partial Task ApplyAsync(IEnumerable<Cookie> cookies);

    // разбираем строку вида "cfsessionhash=abc; cfuserid=42"
    private static IReadOnlyList<Cookie> FromHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return [];
        }

        var cookies = new List<Cookie>();

        foreach (var chunk in header.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var split = chunk.IndexOf('=');

            if (split <= 0)
            {
                continue;
            }

            var name = chunk[..split].Trim();
            var value = chunk[(split + 1)..].Trim();

            if (name.Length > 0)
            {
                cookies.Add(new Cookie(name, value, "/", ForumUrls.Host));
            }
        }

        return cookies;
    }
}
