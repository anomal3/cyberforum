using CyberForum.Core.Http;

namespace CyberForum.Core.Posting;

/// <summary>
/// Всё, что человек делает в теме сам: отвечает, цитирует, говорит спасибо и
/// отмечает лучший ответ. Форма ответа каждый раз берётся у форума свежей —
/// posthash и poststarttime одноразовые, старые движок не примет.
/// </summary>
public sealed class PostingService(ForumHttpClient http)
{
    private readonly ReplyFormParser _parser = new();

    /// <summary>
    /// Форма ответа. Если передать номер сообщения, форум сам положит в неё цитату —
    /// с правильным автором и ссылкой на сообщение, как это делает сайт.
    /// </summary>
    public async Task<ReplyForm?> GetFormAsync(int threadId, int? quotePostId = null, CancellationToken token = default)
    {
        var address = quotePostId is > 0 ? ForumUrls.QuoteReply(quotePostId.Value) : ForumUrls.NewReply(threadId);

        var html = await http.GetStringAsync(address, ForumUrls.Home(), token);

        return _parser.Parse(html, address);
    }

    /// <summary>
    /// Отправка ответа. Просим ajax-ответ: тогда движок отвечает коротким xml,
    /// а не страницей темы, которую он http-клиенту всё равно не отдаст.
    /// </summary>
    public async Task<PostResult> SendAsync(
        ReplyForm form,
        string message,
        bool withSignature = true,
        CancellationToken token = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return PostResult.Failure("Пустое сообщение форум не примет.");
        }

        if (!form.CanPost)
        {
            return PostResult.Failure("Форум не дал формы ответа. Похоже, в этой теме отвечать нельзя.");
        }

        var fields = new Dictionary<string, string>(form.Fields, StringComparer.Ordinal)
        {
            ["do"] = "postreply",
            ["message"] = message,
            ["wysiwyg"] = "0",
            ["parseurl"] = "1",
            ["ajax"] = "1",
            ["sbutton"] = "Отправить",
        };

        if (withSignature)
        {
            fields["signature"] = "1";
        }
        else
        {
            fields.Remove("signature");
        }

        var answer = await http.PostFormAsync(form.Action, fields, ForumUrls.Home(), token);

        var error = ReplyFormParser.ReadError(answer);

        if (!string.IsNullOrEmpty(error))
        {
            return PostResult.Failure(error);
        }

        return ReplyFormParser.LooksAccepted(answer)
            ? PostResult.Success()
            : PostResult.Failure("Форум ответил непонятно — проверь тему, сообщение могло и уйти.");
    }

    /// <summary>
    /// Форма комментария к записи блога. Блог живёт на своих страницах, но устроен
    /// так же, как ответ в тему: те же скрытые поля, то же поле message.
    /// </summary>
    public async Task<ReplyForm?> GetBlogCommentFormAsync(int entryId, CancellationToken token = default)
    {
        var address = ForumUrls.BlogComment(entryId);
        var html = await http.GetStringAsync(address, ForumUrls.Home(), token);

        return _parser.Parse(html, address);
    }

    /// <summary>Форма правки своей записи. Заголовок и текст форум отдаёт уже заполненными.</summary>
    public async Task<ReplyForm?> GetBlogEditFormAsync(int entryId, CancellationToken token = default)
    {
        var address = ForumUrls.BlogEdit(entryId);
        var html = await http.GetStringAsync(address, ForumUrls.Home(), token);

        return _parser.Parse(html, address);
    }

    public Task<PostResult> SendBlogCommentAsync(
        ReplyForm form,
        string message,
        CancellationToken token = default) =>
        SubmitAsync(form, message, null, token);

    public Task<PostResult> SaveBlogEntryAsync(
        ReplyForm form,
        string title,
        string message,
        CancellationToken token = default) =>
        SubmitAsync(form, message, title, token);

    // и комментарий, и правка записи уходят одинаково: берём форму как есть и меняем в ней текст
    private async Task<PostResult> SubmitAsync(
        ReplyForm form,
        string message,
        string? title,
        CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return PostResult.Failure("Пустое сообщение форум не примет.");
        }

        // страховка: на страницах блога рядом живёт форма удаления записи
        if (form.Fields.Keys.Any(name => name.Contains("delete", StringComparison.OrdinalIgnoreCase)))
        {
            return PostResult.Failure("Форум подсунул не ту форму — отправлять её нельзя.");
        }

        var fields = new Dictionary<string, string>(form.Fields, StringComparer.Ordinal)
        {
            ["message"] = message,
            ["wysiwyg"] = "0",
            ["parseurl"] = "1",
        };

        if (title is not null)
        {
            fields["title"] = title;
        }

        var answer = await http.PostFormAsync(form.Action, fields, ForumUrls.Home(), token);
        var error = ReplyFormParser.ReadError(answer);

        if (!string.IsNullOrEmpty(error))
        {
            return PostResult.Failure(error);
        }

        return ReplyFormParser.LooksAccepted(answer)
            ? PostResult.Success()
            : PostResult.Failure("Форум ответил непонятно — проверь запись, изменения могли и сохраниться.");
    }

    /// <summary>Сказать спасибо автору сообщения. Комментарий необязательный, форум его показывает рядом.</summary>
    public async Task<PostResult> ThankAsync(
        int postId,
        string securityToken,
        string? comment = null,
        CancellationToken token = default)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["do"] = "add",
            ["ajax"] = "1",
            ["postid"] = postId.ToString(),
            ["thumb"] = "1",
            ["comment"] = comment ?? string.Empty,
            ["securitytoken"] = securityToken,
        };

        var answer = await http.PostFormAsync(ForumUrls.Thumbs(), fields, ForumUrls.Home(), token);

        return Judge(answer, "Форум не принял спасибо.");
    }

    /// <summary>
    /// Отметить сообщение лучшим ответом или снять отметку. Право на это есть
    /// только у автора темы — чужую тему форум трогать не даст и так и напишет.
    /// </summary>
    public async Task<PostResult> MarkAnswerAsync(
        int threadId,
        int postId,
        string securityToken,
        bool isAnswer = true,
        CancellationToken token = default)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["do"] = isAnswer ? "setanswer" : "removepost",
            ["ajax"] = "1",
            ["t"] = threadId.ToString(),
            ["p"] = postId.ToString(),
            ["securitytoken"] = securityToken,
        };

        if (isAnswer)
        {
            fields["action"] = "is_answer";
        }

        var answer = await http.PostFormAsync(ForumUrls.BestAnswer(), fields, ForumUrls.Home(), token);

        return Judge(answer, "Форум не дал отметить лучший ответ.");
    }

    /// <summary>
    /// Прикрепить файл. Движок принимает вложения отдельным запросом, ещё до
    /// отправки сообщения, и отдаёт номер — его и вставляем в текст как [ATTACH].
    /// </summary>
    public async Task<(UploadedFile? File, ReplyForm Form)> AttachAsync(
        ReplyForm form,
        string fileName,
        byte[] content,
        CancellationToken token = default)
    {
        if (form.AttachmentWindow is not { } window)
        {
            return (null, form);
        }

        var page = await http.GetStringAsync(window, ForumUrls.Home(), token);
        var upload = _parser.ParseAttachmentForm(page);

        if (upload is null)
        {
            return (null, form);
        }

        /* Окно выдаёт собственные posthash и poststarttime — те, что были в форме
           ответа, оно не берёт. Вложение цепляется именно к ним, поэтому дальше
           и сообщение придётся отправлять с этими ключами, иначе файл к нему
           не прицепится и повиснет ничьим.

           Ещё в этом окне две формы в одной: файлом с телефона и ссылкой на файл
           в сети. Что именно от нас хотят, движок понимает по имени нажатой
           кнопки — без «upload» он просто покажет окно заново. */
        var fields = new Dictionary<string, string>(upload.Value.Fields, StringComparer.Ordinal)
        {
            ["upload"] = "Загрузить",
        };

        var answer = await http.PostMultipartAsync(
            upload.Value.Action,
            fields,
            upload.Value.FileField,
            fileName,
            content,
            window,
            token);

        var error = ReplyFormParser.ReadError(answer);

        if (!string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException(error);
        }

        var id = ReplyFormParser.ReadAttachmentId(answer);

        return (id is null ? null : new UploadedFile(id.Value, fileName), Sync(form, fields));
    }

    /// <summary>
    /// Что уже прикреплено к будущему сообщению. Форум помнит вложения по posthash,
    /// поэтому вернувшись в редактор человек должен видеть их, а не пустой список.
    /// </summary>
    public async Task<IReadOnlyList<UploadedFile>> ListAttachmentsAsync(
        ReplyForm form,
        CancellationToken token = default)
    {
        if (form.AttachmentWindow is not { } window)
        {
            return [];
        }

        var page = await http.GetStringAsync(window, ForumUrls.Home(), token);

        return ReplyFormParser.ReadAttachments(page);
    }

    /// <summary>
    /// Открепить файл. Движок удаляет вложения тем же окном: у каждого файла своя
    /// кнопка «Удалить», и номер вложения зашит прямо в её имя.
    /// </summary>
    public async Task<bool> DetachAsync(
        ReplyForm form,
        int attachmentId,
        CancellationToken token = default)
    {
        if (form.AttachmentWindow is not { } window)
        {
            return false;
        }

        var page = await http.GetStringAsync(window, ForumUrls.Home(), token);
        var upload = _parser.ParseAttachmentForm(page);

        if (upload is null)
        {
            return false;
        }

        // у каждого файла в окне своя кнопка удаления, с номером прямо в имени
        var fields = new Dictionary<string, string>(upload.Value.Fields, StringComparer.Ordinal)
        {
            [$"delete[{attachmentId}]"] = "Удалить",
        };

        var answer = await http.PostFormAsync(upload.Value.Action, fields, window, token);

        // получилось, если в списке вложений этого номера больше нет
        return !answer.Contains($"attachmentid={attachmentId}", StringComparison.OrdinalIgnoreCase);
    }

    // переносим ключи окна вложений в форму ответа
    private static ReplyForm Sync(ReplyForm form, IReadOnlyDictionary<string, string> attachment)
    {
        var fields = new Dictionary<string, string>(form.Fields, StringComparer.Ordinal);

        foreach (var key in (string[])["posthash", "poststarttime"])
        {
            if (attachment.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            {
                fields[key] = value;
            }
        }

        return form with { Fields = fields };
    }

    // плагины отвечают крохотным xml: есть <error> — не вышло, нет — приняли
    private static PostResult Judge(string answer, string fallback)
    {
        var error = ReplyFormParser.ReadError(answer);

        if (!string.IsNullOrEmpty(error))
        {
            return PostResult.Failure(error);
        }

        return string.IsNullOrWhiteSpace(answer)
            ? PostResult.Failure(fallback)
            : PostResult.Success();
    }
}
