using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace CyberForum.Core.Posting;

/// <summary>
/// Разбирает формы движка: быстрый ответ прямо в теме, расширенный ответ на
/// newreply.php и окно вложений. Поля не перечисляем поимённо — берём всё, что
/// в форме лежит: движок время от времени добавляет туда новые, и угадывать их
/// список себе дороже.
/// </summary>
public sealed partial class ReplyFormParser
{
    private readonly HtmlParser _parser = new();

    public ReplyForm? Parse(string html, Uri? baseUri = null)
    {
        using var document = _parser.ParseDocument(html);

        /* Ищем форму по полю ввода, а не по адресу: на одной странице их бывает
           несколько, и у блога первой попадается форма удаления записи — отправить
           её вместо правки было бы очень обидно. */
        var form = document.QuerySelector("form#qrform")
                   ?? document.QuerySelectorAll("form")
                       .FirstOrDefault(candidate => candidate.QuerySelector("textarea[name='message']") is not null);

        if (form is null)
        {
            return null;
        }

        var action = ForumUrls.Absolute(form.GetAttribute("action")) ?? baseUri;

        if (action is null)
        {
            return null;
        }

        var fields = ReadFields(form);
        var message = form.QuerySelector("textarea[name='message']")?.TextContent ?? string.Empty;

        return new ReplyForm
        {
            Action = action,
            Fields = fields,
            Message = message.Trim('\r', '\n'),
            AttachmentUrl = FindAttachmentWindow(document, html),
        };
    }

    /// <summary>Форма окна вложений: там своё поле для файла и свои скрытые поля.</summary>
    public (Uri Action, IReadOnlyDictionary<string, string> Fields, string FileField)? ParseAttachmentForm(string html)
    {
        using var document = _parser.ParseDocument(html);

        var file = document.QuerySelector("input[type='file'][name]");
        var form = file?.Closest("form") ?? document.QuerySelector("form[action*='newattachment.php']");

        if (form is null)
        {
            return null;
        }

        var action = ForumUrls.Absolute(form.GetAttribute("action"));

        if (action is null)
        {
            return null;
        }

        var name = file?.GetAttribute("name") ?? "attachment[]";

        return (action, ReadFields(form), name);
    }

    private static Dictionary<string, string> ReadFields(IElement form)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var input in form.QuerySelectorAll("input[name]"))
        {
            var name = input.GetAttribute("name");

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var type = (input.GetAttribute("type") ?? "text").ToLowerInvariant();

            // кнопки отправки и файлы форме нужны, а нам нет: первую подставим сами,
            // второй уедет отдельным запросом
            if (type is "submit" or "button" or "image" or "reset" or "file")
            {
                continue;
            }

            // невыбранная галочка на форуме значит «нет», и отправлять её нельзя
            if (type is "checkbox" or "radio" && input.GetAttribute("checked") is null)
            {
                continue;
            }

            fields[name] = input.GetAttribute("value") ?? string.Empty;
        }

        foreach (var select in form.QuerySelectorAll("select[name]"))
        {
            var name = select.GetAttribute("name");
            var chosen = select.QuerySelector("option[selected]") ?? select.QuerySelector("option");

            if (!string.IsNullOrEmpty(name) && chosen is not null)
            {
                fields[name] = chosen.GetAttribute("value") ?? string.Empty;
            }
        }

        return fields;
    }

    /* Кнопку вложений форум рисует скриптом, а без скриптов оставляет обычную
       ссылку в noscript — её и берём: в адресе уже лежат posthash и poststarttime. */
    private static Uri? FindAttachmentWindow(IDocument document, string html)
    {
        var link = document.QuerySelector("a[href*='newattachment.php']")?.GetAttribute("href");

        if (!string.IsNullOrEmpty(link))
        {
            return ForumUrls.Absolute(link);
        }

        var match = AttachmentWindowRegex().Match(html);

        return match.Success ? ForumUrls.Absolute(match.Groups[1].Value.Replace("&amp;", "&")) : null;
    }

    /// <summary>
    /// Что форум ответил на отправку. В ajax-режиме это маленький xml, а если
    /// что-то не понравилось — обычная страница с перечнем ошибок.
    /// </summary>
    public static string? ReadError(string answer)
    {
        if (string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        var xml = XmlErrorRegex().Match(answer);

        if (xml.Success)
        {
            return Clean(xml.Groups[1].Value);
        }

        var block = HtmlErrorRegex().Match(answer);

        return block.Success ? Clean(block.Groups[1].Value) : null;
    }

    /// <summary>Отправку приняли, если форум показал сообщение или собрался нас увести к теме.</summary>
    public static bool LooksAccepted(string answer) =>
        answer.Contains("<postbit", StringComparison.OrdinalIgnoreCase) ||
        answer.Contains("Ваше сообщение", StringComparison.OrdinalIgnoreCase) ||
        answer.Contains("Сообщение отправлено", StringComparison.OrdinalIgnoreCase) ||
        answer.Contains("Ваш комментарий", StringComparison.OrdinalIgnoreCase) ||
        answer.Contains("Запись сохранена", StringComparison.OrdinalIgnoreCase) ||
        RedirectRegex().IsMatch(answer);

    /// <summary>Номер только что загруженного вложения — его вставляем в текст как [ATTACH].</summary>
    public static int? ReadAttachmentId(string answer)
    {
        var matches = AttachmentIdRegex().Matches(answer);

        if (matches.Count == 0)
        {
            return null;
        }

        // окно показывает все вложения сообщения, наше — последнее
        return int.Parse(matches[^1].Groups[1].Value);
    }

    /// <summary>
    /// Список уже прикреплённых файлов со страницы окна вложений: имя берём
    /// из ссылки, а номер — из имени кнопки удаления рядом с ней.
    /// </summary>
    public static IReadOnlyList<UploadedFile> ReadAttachments(string page)
    {
        var files = new List<UploadedFile>();

        foreach (Match match in AttachmentRowRegex().Matches(page))
        {
            var id = int.Parse(match.Groups["id"].Value);

            if (files.Any(file => file.AttachmentId == id))
            {
                continue;
            }

            files.Add(new UploadedFile(id, Clean(match.Groups["name"].Value)));
        }

        return files;
    }

    private static string Clean(string raw)
    {
        var text = TagRegex().Replace(raw, " ");

        text = text
            .Replace("&nbsp;", " ")
            .Replace("&quot;", "\"")
            .Replace("&#039;", "'")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&amp;", "&");

        return SpacesRegex().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"open_window\('(newattachment\.php\?[^']+)'", RegexOptions.IgnoreCase)]
    private static partial Regex AttachmentWindowRegex();

    [GeneratedRegex(@"<error[^>]*>(.*?)</error>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex XmlErrorRegex();

    [GeneratedRegex(@"class=""(?:standard_error|blockrow error)""[^>]*>(.*?)</(?:div|td)>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex HtmlErrorRegex();

    [GeneratedRegex(@"attachmentid=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex AttachmentIdRegex();

    // строка окна вложений: ссылка на файл, а следом кнопка delete[номер]
    [GeneratedRegex(
        @"attachmentid=(?<id>\d+)[^>]*>(?<name>[^<]+)</a>.*?name=""delete\[\k<id>\]""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex AttachmentRowRegex();

    [GeneratedRegex(@"http-equiv=""Refresh""", RegexOptions.IgnoreCase)]
    private static partial Regex RedirectRegex();

    [GeneratedRegex(@"<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex SpacesRegex();
}
