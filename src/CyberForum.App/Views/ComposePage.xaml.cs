using CyberForum.App.Services;
using CyberForum.Core.Posting;

namespace CyberForum.App.Views;

/// <summary>
/// Полный ответ: оформление, блоки кода с подсветкой и вложения. Всё, что на сайте
/// разбросано по полусотне мелких кнопок, здесь собрано в одну полосу внизу —
/// пальцем по картинкам размером с ноготь не попасть.
/// </summary>
public partial class ComposePage : ContentPage
{
    private readonly PostingService _posting;
    private readonly ReplyContext _reply;

    public ComposePage(PostingService posting, ReplyContext reply)
    {
        InitializeComponent();

        _posting = posting;
        _reply = reply;

        BuildLanguages();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        ThreadName.Text = _reply.ThreadTitle;
        Body.Text = _reply.Draft;

        // у записи блога правится ещё и заголовок, у ответа его просто нет
        var entry = _reply.Target == ReplyTarget.BlogEntry;

        TitleBox.IsVisible = entry;
        EntryTitle.Text = _reply.EntryTitle;

        Title = _reply.Target switch
        {
            ReplyTarget.BlogComment => "Комментарий",
            ReplyTarget.BlogEntry => "Правка записи",
            _ => "Ответ в тему",
        };

        // вложения движок принимает только для сообщений на форуме
        PhotoButton.IsVisible = FileButton.IsVisible = _reply.Target == ReplyTarget.Thread;

        ShowFiles();
        Body.Focus();

        if (_reply.Target == ReplyTarget.Thread)
        {
            RefreshFilesAsync();
        }
    }

    /// <summary>
    /// Спрашиваем у форума, что уже висит на этом черновике. Файл мог остаться
    /// с прошлого захода — человек должен его видеть, а не удивляться потом.
    /// </summary>
    private async void RefreshFilesAsync()
    {
        try
        {
            if (await FormAsync() is not { } form)
            {
                return;
            }

            var known = await _posting.ListAttachmentsAsync(form);

            foreach (var file in known)
            {
                if (_reply.Files.All(mine => mine.AttachmentId != file.AttachmentId))
                {
                    _reply.Files.Add(file);
                }
            }

            ShowFiles();
        }
        catch (Exception)
        {
            // не ответил — покажем только то, что прикрепили в этот раз
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        // текст не теряем: человек мог просто вернуться в тему что-то перечитать
        _reply.Draft = Body.Text ?? string.Empty;
        _reply.EntryTitle = EntryTitle.Text ?? string.Empty;
    }

    // ——— оформление ———

    private void OnBoldClicked(object? sender, EventArgs e) => Wrap("[B]", "[/B]");

    private void OnItalicClicked(object? sender, EventArgs e) => Wrap("[I]", "[/I]");

    private void OnQuoteClicked(object? sender, EventArgs e) => Wrap("[QUOTE]", "[/QUOTE]");

    private void OnSpoilerClicked(object? sender, EventArgs e) => Wrap("[SPOILER]", "[/SPOILER]");

    private void OnLinkClicked(object? sender, EventArgs e) => Wrap("[URL=]", "[/URL]");

    private void OnListClicked(object? sender, EventArgs e) => Wrap("[LIST]\n[*]", "\n[/LIST]");

    private void OnCodeClicked(object? sender, EventArgs e) =>
        LanguageBox.IsVisible = !LanguageBox.IsVisible;

    /* Языков на форуме под полсотни, и все они — отдельные bb-коды. Раскладываем
       их в два ряда с прокруткой вбок: список длинный, зато язык выбирается
       одним касанием, а ходовые лежат в самом начале. */
    private void BuildLanguages()
    {
        for (var i = 0; i < CodeLanguages.All.Count; i++)
        {
            var language = CodeLanguages.All[i];

            var chip = new Button
            {
                Text = language.Title,
                Style = Style("ToolChip"),
            };

            chip.Clicked += (_, _) => InsertCode(language);

            if (i % 2 == 0)
            {
                LanguagesTop.Children.Add(chip);
            }
            else
            {
                LanguagesBottom.Children.Add(chip);
            }
        }
    }

    private void InsertCode(CodeLanguage language)
    {
        LanguageBox.IsVisible = false;

        var selected = Selected();

        Replace(CodeLanguages.Wrap(language, selected), language.Tag.Length + 3);
    }

    // ——— вложения ———

    private async void OnPhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            var picked = await MediaPicker.Default.PickPhotoAsync();

            if (picked is not null)
            {
                await UploadAsync(picked.FileName, await picked.OpenReadAsync());
            }
        }
        catch (Exception error)
        {
            Say("Не вышло взять картинку: " + error.Message, true);
        }
    }

    private async void OnFileClicked(object? sender, EventArgs e)
    {
        try
        {
            var picked = await FilePicker.Default.PickAsync();

            if (picked is not null)
            {
                await UploadAsync(picked.FileName, await picked.OpenReadAsync());
            }
        }
        catch (Exception error)
        {
            Say("Не вышло взять файл: " + error.Message, true);
        }
    }

    /// <summary>
    /// Вложение уходит на форум отдельно от сообщения — движок иначе не умеет.
    /// В ответ он даёт номер, по которому файл потом встаёт в текст.
    /// </summary>
    private async Task UploadAsync(string name, Stream data)
    {
        SetBusy(true, "Отправляем файл…");

        try
        {
            var form = await FormAsync();

            if (form?.AttachmentWindow is null)
            {
                Say("Форум не дал прикрепить файл к этой теме.", true);
                return;
            }

            using var memory = new MemoryStream();
            await data.CopyToAsync(memory);
            await data.DisposeAsync();

            var (file, updated) = await _posting.AttachAsync(form, name, memory.ToArray());

            // форма могла обновиться: ключи вложения должны уехать вместе с сообщением
            _reply.Form = updated;

            if (file is null)
            {
                Say("Файл не прикрепился. Возможно, форум не принимает такой тип.", true);
                return;
            }

            _reply.Files.Add(file);

            ShowFiles();
            Replace($"[ATTACH]{file.AttachmentId}[/ATTACH]\n", null);

            Say($"Файл «{file.Name}» прикреплён", false);
        }
        catch (Exception error)
        {
            Say("Не получилось отправить файл: " + error.Message, true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowFiles()
    {
        FilesList.Children.Clear();
        FilesBox.IsVisible = _reply.Files.Count > 0;

        foreach (var file in _reply.Files)
        {
            var row = new HorizontalStackLayout { Spacing = 8 };

            row.Children.Add(new Microsoft.Maui.Controls.Shapes.Path
            {
                Data = (Microsoft.Maui.Controls.Shapes.Geometry)Application.Current!.Resources["IconAttach"],
                Style = Style("IconMark"),
            });

            row.Children.Add(new Label
            {
                Text = file.Name,
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
            });

            var id = file.AttachmentId;

            var again = new Label
            {
                Text = "вставить",
                FontSize = 13,
                TextColor = Color.FromArgb("#0092ca"),
                VerticalOptions = LayoutOptions.Center,
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => Replace($"[ATTACH]{id}[/ATTACH]\n", null);
            again.GestureRecognizers.Add(tap);

            // прикрепил не то — убрать файл можно тут же, не выходя из редактора
            var drop = new Label
            {
                Text = "убрать",
                FontSize = 13,
                TextColor = Color.FromArgb("#b3261e"),
                VerticalOptions = LayoutOptions.Center,
            };

            var dropTap = new TapGestureRecognizer();
            dropTap.Tapped += (_, _) => DetachAsync(file);
            drop.GestureRecognizers.Add(dropTap);

            row.Children.Add(again);
            row.Children.Add(drop);
            FilesList.Children.Add(row);
        }
    }

    private async void DetachAsync(UploadedFile file)
    {
        if (_reply.Form is not { } form)
        {
            return;
        }

        SetBusy(true, "Убираем файл…");

        try
        {
            var gone = await _posting.DetachAsync(form, file.AttachmentId);

            if (!gone)
            {
                Say("Форум файл не отдал обратно. Попробуй ещё раз.", true);
                return;
            }

            _reply.Files.Remove(file);

            // из текста тоже убираем, иначе останется ссылка в никуда
            Body.Text = (Body.Text ?? string.Empty)
                .Replace($"[ATTACH]{file.AttachmentId}[/ATTACH]\n", string.Empty)
                .Replace($"[ATTACH]{file.AttachmentId}[/ATTACH]", string.Empty);

            ShowFiles();
            Say($"Файл «{file.Name}» убран", false);
        }
        catch (Exception error)
        {
            Say("Не получилось убрать файл: " + error.Message, true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    // ——— отправка ———

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var text = (Body.Text ?? string.Empty).Trim();

        if (text.Length == 0)
        {
            Say("Пустое сообщение форум не примет.", true);
            return;
        }

        SetBusy(true, "Отправляем…");

        try
        {
            var form = await FormAsync();

            if (form is null)
            {
                Say("Форум не дал формы. Попробуй войти заново.", true);
                return;
            }

            var result = _reply.Target switch
            {
                ReplyTarget.BlogComment => await _posting.SendBlogCommentAsync(form, text),
                ReplyTarget.BlogEntry => await _posting.SaveBlogEntryAsync(form, (EntryTitle.Text ?? string.Empty).Trim(), text),
                _ => await _posting.SendAsync(form, text),
            };

            if (!result.Ok)
            {
                Say(result.Message ?? "Форум ответ не принял.", true);
                return;
            }

            // ключи в форме одноразовые, тема сама перечитает себя при возврате
            _reply.Form = null;
            _reply.Posted = true;
            _reply.Clear();

            await Shell.Current.GoToAsync("..");
        }
        catch (Exception error)
        {
            Say("Не получилось отправить: " + error.Message, true);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task<ReplyForm?> FormAsync() =>
        _reply.Form ??= _reply.Target switch
        {
            ReplyTarget.BlogComment => await _posting.GetBlogCommentFormAsync(_reply.EntryId),
            ReplyTarget.BlogEntry => await _posting.GetBlogEditFormAsync(_reply.EntryId),
            _ => await _posting.GetFormAsync(_reply.ThreadId),
        };

    // ——— мелочи ———

    // стили лежат в общем словаре приложения, отсюда их и берём
    private static Style Style(string key) => (Style)Application.Current!.Resources[key];

    /// <summary>Оборачивает выделенное или ставит пустую пару и уводит курсор внутрь.</summary>
    private void Wrap(string before, string after)
    {
        var selected = Selected();

        Replace(before + selected + after, before.Length + selected.Length);
    }

    private string Selected()
    {
        var text = Body.Text ?? string.Empty;
        var start = Math.Clamp(Body.CursorPosition, 0, text.Length);
        var length = Math.Clamp(Body.SelectionLength, 0, text.Length - start);

        return text.Substring(start, length);
    }

    private void Replace(string insert, int? caretInside)
    {
        var text = Body.Text ?? string.Empty;
        var start = Math.Clamp(Body.CursorPosition, 0, text.Length);
        var length = Math.Clamp(Body.SelectionLength, 0, text.Length - start);

        Body.Text = text[..start] + insert + text[(start + length)..];
        Body.CursorPosition = start + (caretInside ?? insert.Length);
        Body.Focus();
    }

    private void SetBusy(bool going, string? what = null)
    {
        Busy.IsVisible = Busy.IsRunning = going;
        SendButton.IsEnabled = !going;

        if (going && what is not null)
        {
            Say(what, false);
        }
    }

    private void Say(string message, bool bad)
    {
        Hint.Text = message;
        Hint.IsVisible = true;
        Hint.TextColor = bad
            ? Color.FromArgb("#b3261e")
            : (Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#9aa4ae")
                : Color.FromArgb("#5f6b76"));
    }
}
