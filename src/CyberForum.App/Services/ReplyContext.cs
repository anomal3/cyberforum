using System.Text.Json;
using CyberForum.Core.Posting;

namespace CyberForum.App.Services;

/// <summary>Куда пишем: ответ в тему, комментарий к записи блога или правка самой записи.</summary>
public enum ReplyTarget
{
    Thread,
    BlogComment,
    BlogEntry,
}

/// <summary>
/// Черновик ответа, общий на приложение. Нужен, потому что ответ живёт в двух местах
/// сразу: начали в шторке внизу темы, а дописывают уже в полном редакторе — и текст
/// с вложениями между ними терять нельзя.
/// </summary>
public sealed class ReplyContext
{
    public ReplyTarget Target { get; private set; } = ReplyTarget.Thread;

    public int ThreadId { get; private set; }

    /// <summary>Номер записи блога — для комментария и для правки.</summary>
    public int EntryId { get; private set; }

    /// <summary>Заголовок записи блога: его правят вместе с текстом.</summary>
    public string EntryTitle { get; set; } = string.Empty;

    public string ThreadTitle { get; private set; } = string.Empty;

    public ReplyForm? Form { get; set; }

    public string Draft { get; set; } = string.Empty;

    public int? QuotePostId { get; set; }

    public string? QuoteAuthor { get; set; }

    public List<UploadedFile> Files { get; } = [];

    /// <summary>Полный редактор отправил ответ — теме пора перечитать себя.</summary>
    public bool Posted { get; set; }

    // тема сменилась — старый черновик к ней уже не относится
    public void Reset(int threadId, string title)
    {
        if (Target == ReplyTarget.Thread && ThreadId == threadId)
        {
            ThreadTitle = title;
            return;
        }

        Forget();

        Target = ReplyTarget.Thread;
        ThreadId = threadId;
        ThreadTitle = title;
    }

    /// <summary>Перешли к блогу: комментарий к записи или правка её самой.</summary>
    public void ResetBlog(ReplyTarget target, int entryId, string title)
    {
        if (Target == target && EntryId == entryId)
        {
            ThreadTitle = title;
            return;
        }

        Forget();

        Target = target;
        EntryId = entryId;
        ThreadTitle = title;
    }

    private void Forget()
    {
        Form = null;
        Draft = string.Empty;
        EntryTitle = string.Empty;
        QuotePostId = null;
        QuoteAuthor = null;
        Files.Clear();
        Posted = false;
    }

    public void Clear()
    {
        Draft = string.Empty;
        QuotePostId = null;
        QuoteAuthor = null;
        Files.Clear();
    }

    /// <summary>
    /// Форма ответа, снятая читалкой прямо со страницы темы. Так дешевле и надёжнее,
    /// чем идти за ней на сайт ещё раз: страница уже открыта, поля в ней свежие.
    /// </summary>
    public static ReplyForm? FromPage(string? json)
    {
        var text = Unwrap(json);

        if (string.IsNullOrWhiteSpace(text) || text == "null")
        {
            return null;
        }

        try
        {
            // разбираем руками: в обрезанной сборке разбор по типам легко теряет свойства
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;

            if (!root.TryGetProperty("action", out var action) ||
                !root.TryGetProperty("fields", out var fields))
            {
                return null;
            }

            var address = CyberForum.Core.ForumUrls.Absolute(action.GetString());

            if (address is null)
            {
                return null;
            }

            var values = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var field in fields.EnumerateObject())
            {
                values[field.Name] = field.Value.GetString() ?? string.Empty;
            }

            /* Без одноразового ключа форма бесполезна: значит, читалка успела снять
               её раньше, чем страница дописала низ. Пусть тогда приложение сходит
               за формой само, а не отправляет заведомо негодную. */
            if (!values.ContainsKey("posthash"))
            {
                return null;
            }

            var attach = root.TryGetProperty("attach", out var link) ? link.GetString() : null;

            return new ReplyForm
            {
                Action = address,
                Fields = values,
                AttachmentUrl = CyberForum.Core.ForumUrls.Absolute(attach),
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /* Android отдаёт результат скрипта уже завёрнутым в json-строку: сначала
       разворачиваем обёртку, а внутри лежит наш объект. */
    private static string? Unwrap(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith('"'))
        {
            return value;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
