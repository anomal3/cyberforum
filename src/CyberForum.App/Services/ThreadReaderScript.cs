using System.Text;
using CyberForum.Core.Rendering;

namespace CyberForum.App.Services;

// Готовит скрипт-читалку: подставляет в него наши стили и отдаёт одной строкой,
// которую можно скормить WebView. Файл читаем один раз и держим наготове,
// чтобы в момент показа темы не ждать диска.
public sealed class ThreadReaderScript(SessionService session)
{
    private string? _source;
    private string? _name;

    public string? Script { get; private set; }

    public async Task<string> PrepareAsync()
    {
        if (_source is null)
        {
            await using var stream = await FileSystem.OpenAppPackageFileAsync("reader.js");
            using var reader = new StreamReader(stream);

            var text = await reader.ReadToEndAsync();

            // css передаём в base64: так его не поломают кавычки и переносы строк
            var css = Convert.ToBase64String(Encoding.UTF8.GetBytes(ThreadStyles.Default));

            _source = text.Replace("%%CSS%%", css);
        }

        var name = session.Current.UserName ?? string.Empty;

        if (Script is not null && _name == name)
        {
            return Script;
        }

        /* Имя вошедшего читалке нужно, чтобы понять, где чьё сообщение: у своих
           форум рисует имя простым текстом, без ссылки на профиль, и номер
           пользователя оттуда не достать. */
        var body = _source.Replace("%%ME%%", name.Replace("'", "\\'"));

        // Скрипт целиком тоже прячем в base64 и запускаем через eval: WebView получает
        // код одной строкой, иначе переносы схлопываются и однострочные комментарии
        // сжирают всё, что идёт за ними.
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));

        _name = name;

        return Script = $"eval(decodeURIComponent(escape(atob('{payload}'))))";
    }
}
