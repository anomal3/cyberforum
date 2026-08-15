using System.Net;
using CyberForum.Core;
using WebKit;

namespace CyberForum.App.Services;

public sealed partial class CookieHarvester
{
    public partial async Task<IReadOnlyList<Cookie>> CollectAsync()
    {
        var store = WKWebsiteDataStore.DefaultDataStore.HttpCookieStore;
        var found = await store.GetAllCookiesAsync();

        var cookies = new List<Cookie>();

        foreach (var cookie in found)
        {
            if (!cookie.Domain.TrimStart('.').EndsWith("cyberforum.ru", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            cookies.Add(new Cookie(cookie.Name, cookie.Value, "/", ForumUrls.Host));
        }

        return cookies;
    }

    public partial async Task ApplyAsync(IEnumerable<Cookie> cookies)
    {
        var store = WKWebsiteDataStore.DefaultDataStore.HttpCookieStore;

        foreach (var cookie in cookies)
        {
            var ready = new Foundation.NSHttpCookie(new Foundation.NSDictionary(
                Foundation.NSHttpCookie.KeyName, cookie.Name,
                Foundation.NSHttpCookie.KeyValue, cookie.Value,
                Foundation.NSHttpCookie.KeyDomain, "." + ForumUrls.Host,
                Foundation.NSHttpCookie.KeyPath, "/"));

            await store.SetCookieAsync(ready);
        }
    }
}
