using System.Net;
using System.Text.Json;
using CyberForum.Core;
using CyberForum.Core.Models;

namespace CyberForum.App.Services;

/// <summary>
/// Держит вход живым между запусками: забирает куки из WebView, скармливает их
/// http-клиенту и прячет в защищённое хранилище, чтобы не входить заново каждый раз.
/// </summary>
public sealed class SessionService(ForumClient client, CookieHarvester harvester)
{
    private const string StorageKey = "cyberforum.cookies";

    public SessionState Current { get; private set; } = new();

    public event EventHandler<SessionState>? Changed;

    // зовём на старте приложения
    public async Task RestoreAsync()
    {
        try
        {
            var saved = await SecureStorage.Default.GetAsync(StorageKey);

            if (!string.IsNullOrEmpty(saved))
            {
                var pairs = JsonSerializer.Deserialize<Dictionary<string, string>>(saved);

                if (pairs is not null)
                {
                    var cookies = pairs
                        .Select(pair => new Cookie(pair.Key, pair.Value, "/", ForumUrls.Host))
                        .ToList();

                    client.Http.ApplyCookies(cookies);

                    // WebView живёт своей жизнью и про наши куки не знает —
                    // без этого тема откроется глазами гостя
                    await harvester.ApplyAsync(cookies);
                }
            }
        }
        catch (Exception)
        {
            // не смогли прочитать хранилище — просто останемся гостем
        }

        await RefreshAsync();
    }

    /// <summary>
    /// Вход своей формой. Куки после него живут в http-клиенте, но тему показывает
    /// WebView — ему их надо отдать отдельно, иначе он останется гостем.
    /// </summary>
    public async Task<(bool Ok, string? Message, SessionState State)> SignInAsync(
        string user,
        string password,
        CancellationToken token = default)
    {
        var login = new LoginService(client.Http);
        var result = await login.SignInAsync(user, password, token);

        if (!result.Ok)
        {
            return (false, result.Message, Current);
        }

        var cookies = client.Http.Cookies.GetCookies(ForumUrls.Base).ToList();

        await harvester.ApplyAsync(cookies);
        await SaveAsync(cookies);

        var state = await RefreshAsync();

        return state.IsAuthenticated
            ? (true, null, state)
            : (false, "Форум нас не узнал. Попробуй войти через его страницу.", state);
    }

    // вызывается после того, как человек прошёл вход в WebView
    public async Task<SessionState> AdoptWebViewSessionAsync()
    {
        var cookies = await harvester.CollectAsync();

        if (cookies.Count > 0)
        {
            client.Http.ApplyCookies(cookies);
            await SaveAsync(cookies);
        }

        return await RefreshAsync();
    }

    public async Task<SessionState> RefreshAsync()
    {
        try
        {
            Current = await client.GetSessionAsync();
        }
        catch (Exception)
        {
            Current = new SessionState();
        }

        Changed?.Invoke(this, Current);
        return Current;
    }

    public Task ForgetAsync()
    {
        SecureStorage.Default.Remove(StorageKey);
        Current = new SessionState();
        Changed?.Invoke(this, Current);

        return Task.CompletedTask;
    }

    private static async Task SaveAsync(IReadOnlyList<Cookie> cookies)
    {
        // Одно и то же имя приходит по нескольку раз: у форума свои куки лежат и на
        // www.cyberforum.ru, и на .cyberforum.ru, а служебные вроде adrcid плодятся
        // на каждом поддомене. ToDictionary на таком падал и ронял всё приложение —
        // берём последнее значение, оно свежее.
        var pairs = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var cookie in cookies)
        {
            if (!string.IsNullOrEmpty(cookie.Name))
            {
                pairs[cookie.Name] = cookie.Value;
            }
        }

        await SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(pairs));
    }
}
