using System.Text;
using CyberForum.Core.Rendering;

namespace CyberForum.App.Services;

// Готовит скрипт-читалку: подставляет в него наши стили и отдаёт одной строкой,
// которую можно скормить WebView. Читаем файл один раз и держим наготове,
// чтобы в момент показа темы не ждать диска.
public sealed class ThreadReaderScript
{
    public string? Script { get; private set; }

    public async Task<string> PrepareAsync()
    {
        if (Script is not null)
        {
            return Script;
        }

        await using var stream = await FileSystem.OpenAppPackageFileAsync("reader.js");
        using var reader = new StreamReader(stream);

        var source = await reader.ReadToEndAsync();

        // css передаём в base64: так его не поломают кавычки и переносы строк
        var css = Convert.ToBase64String(Encoding.UTF8.GetBytes(ThreadStyles.Default));
        var body = source.Replace("%%CSS%%", css);

        // Скрипт целиком тоже прячем в base64 и запускаем через eval: WebView получает
        // код одной строкой, иначе переносы схлопываются и однострочные комментарии
        // сжирают всё, что идёт за ними.
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));

        return Script = $"eval(decodeURIComponent(escape(atob('{payload}'))))";
    }
}
