namespace CyberForum.Core.Posting;

/// <summary>
/// Форма ответа, снятая со страницы форума. Ничего своего мы не выдумываем: движок
/// кладёт в неё одноразовые posthash и poststarttime, и без них ответ не примут.
/// </summary>
public sealed record ReplyForm
{
    public required Uri Action { get; init; }

    public required IReadOnlyDictionary<string, string> Fields { get; init; }

    /// <summary>
    /// Что форум уже положил в поле ввода. При ответе с цитатой там лежит готовый
    /// [QUOTE=автор;номер] — собирать его самим не нужно.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Адрес окна вложений — с ним же приезжают posthash и poststarttime.</summary>
    public Uri? AttachmentUrl { get; init; }

    public int ThreadId => Number("t");

    public string SecurityToken => Fields.GetValueOrDefault("securitytoken", string.Empty);

    /// <summary>Заголовок — он есть у записи блога, у ответа в тему его нет.</summary>
    public string EntryTitle => Fields.GetValueOrDefault("title", string.Empty);

    public bool CanPost => Fields.ContainsKey("posthash") && Fields.ContainsKey("securitytoken");

    /// <summary>
    /// Куда отправлять файлы. Ссылку на это окно форум прячет в noscript, а браузер
    /// такое содержимое за разметку не считает — поэтому, если её не нашлось,
    /// собираем адрес сами: ключи для него лежат в самой форме.
    /// </summary>
    public Uri? AttachmentWindow
    {
        get
        {
            if (AttachmentUrl is not null)
            {
                return AttachmentUrl;
            }

            var hash = Fields.GetValueOrDefault("posthash");
            var started = Fields.GetValueOrDefault("poststarttime");

            return string.IsNullOrEmpty(hash) || string.IsNullOrEmpty(started) || ThreadId <= 0
                ? null
                : ForumUrls.NewAttachment(ThreadId, hash, started);
        }
    }

    private int Number(string key) =>
        int.TryParse(Fields.GetValueOrDefault(key), out var value) ? value : 0;
}

/// <summary>Чем закончилась отправка. Текст ошибки берём тот, что написал форум.</summary>
public sealed record PostResult(bool Ok, string? Message)
{
    public static PostResult Success(string? message = null) => new(true, message);

    public static PostResult Failure(string message) => new(false, message);
}

/// <summary>Прикреплённый файл: id нужен, чтобы вставить [ATTACH] в текст сообщения.</summary>
public sealed record UploadedFile(int AttachmentId, string Name);
