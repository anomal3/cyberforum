using System.Net;

namespace CyberForum.App.Services;

// На Windows приложение живёт как побочная сборка, полноценный вход тут не нужен —
// возвращаем пусто, чтобы проект собирался и работал в гостевом режиме.
public sealed partial class CookieHarvester
{
    public partial Task<IReadOnlyList<Cookie>> CollectAsync() =>
        Task.FromResult<IReadOnlyList<Cookie>>([]);

    public partial Task ApplyAsync(IEnumerable<Cookie> cookies) => Task.CompletedTask;
}
