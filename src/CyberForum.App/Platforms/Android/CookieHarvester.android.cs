using System.Net;
using Android.Webkit;
using CyberForum.Core;

namespace CyberForum.App.Services;

public sealed partial class CookieHarvester
{
    public partial Task<IReadOnlyList<Cookie>> CollectAsync()
    {
        var manager = CookieManager.Instance;
        manager?.Flush();

        var header = manager?.GetCookie(ForumUrls.Base.ToString());

        return Task.FromResult(FromHeader(header));
    }

    public partial Task ApplyAsync(IEnumerable<Cookie> cookies)
    {
        var manager = CookieManager.Instance;

        if (manager is null)
        {
            return Task.CompletedTask;
        }

        manager.SetAcceptCookie(true);

        var address = ForumUrls.Base.ToString();

        foreach (var cookie in cookies)
        {
            // домен с точкой впереди — чтобы куки работали и на www, и без него
            manager.SetCookie(address, $"{cookie.Name}={cookie.Value}; domain=.{ForumUrls.Host}; path=/");
        }

        manager.Flush();

        return Task.CompletedTask;
    }
}
