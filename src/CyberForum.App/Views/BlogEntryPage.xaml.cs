using CyberForum.App.Services;
using CyberForum.App.ViewModels;
using CyberForum.Core.Posting;

namespace CyberForum.App.Views;

public partial class BlogEntryPage : ContentPage
{
    private readonly BlogEntryViewModel _viewModel;
    private readonly DownloadService _downloads;
    private readonly PostingService _posting;
    private readonly ReplyContext _reply;
    private readonly SessionService _session;

    public BlogEntryPage(
        BlogEntryViewModel viewModel,
        DownloadService downloads,
        PostingService posting,
        ReplyContext reply,
        SessionService session)
    {
        InitializeComponent();

        BindingContext = _viewModel = viewModel;
        _downloads = downloads;
        _posting = posting;
        _reply = reply;
        _session = session;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // комментировать можно только вошедшим — гостю форма всё равно не достанется
        CommentBar.IsVisible = _session.Current.IsAuthenticated;

        Sheet.Placeholder = "Комментарий к записи";
        Sheet.ShowFullEditor = true;

        // вернулись из полного редактора, а он уже всё отправил — перечитываем запись
        if (_reply.Posted)
        {
            _reply.Posted = false;
            _reply.Clear();

            _viewModel.ReloadCommand.Execute(null);
        }
    }

    private void OnCommentBarTapped(object? sender, TappedEventArgs e)
    {
        _reply.ResetBlog(ReplyTarget.BlogComment, _viewModel.EntryId, _viewModel.Title);

        Sheet.Text = _reply.Draft;
        CommentBar.IsVisible = false;
        Sheet.Open();
    }

    private void OnSheetClosed(object? sender, EventArgs e)
    {
        _reply.Draft = Sheet.Text;
        CommentBar.IsVisible = _session.Current.IsAuthenticated;
    }

    protected override bool OnBackButtonPressed()
    {
        if (!Sheet.IsOpen)
        {
            return base.OnBackButtonPressed();
        }

        Sheet.Close();
        return true;
    }

    private async void OnSendClicked(object? sender, EventArgs e)
    {
        var text = Sheet.Text.Trim();

        if (text.Length == 0)
        {
            Sheet.Say("Пустой комментарий форум не примет.");
            return;
        }

        Sheet.SetBusy(true);

        try
        {
            var form = _reply.Form ??= await _posting.GetBlogCommentFormAsync(_viewModel.EntryId);

            if (form is null)
            {
                Sheet.Say("Форум не дал формы комментария.");
                return;
            }

            var result = await _posting.SendBlogCommentAsync(form, text);

            if (!result.Ok)
            {
                Sheet.Say(result.Message ?? "Форум комментарий не принял.");
                return;
            }

            Sheet.Text = string.Empty;
            _reply.Clear();
            _reply.Form = null;

            Sheet.Close();

            // запись перечитываем: комментарий должен встать в общий список
            _viewModel.ReloadCommand.Execute(null);
        }
        catch (Exception error)
        {
            Sheet.Say("Не получилось отправить: " + error.Message);
        }
        finally
        {
            Sheet.SetBusy(false);
        }
    }

    private async void OnFullEditorClicked(object? sender, EventArgs e)
    {
        _reply.Draft = Sheet.Text;
        _reply.Form ??= await _posting.GetBlogCommentFormAsync(_viewModel.EntryId);

        Sheet.Close();

        await Shell.Current.GoToAsync(Routes.ToCompose());
    }

    /// <summary>
    /// Правка своей записи. Заголовок и текст форум отдаёт уже заполненными —
    /// открываем их в том же редакторе, что и ответы.
    /// </summary>
    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (!_viewModel.IsMine)
        {
            _viewModel.ErrorMessage = "Править можно только свои записи.";
            return;
        }

        _reply.ResetBlog(ReplyTarget.BlogEntry, _viewModel.EntryId, _viewModel.Title);

        try
        {
            _viewModel.ErrorMessage = "Открываем запись…";

            var form = _reply.Form ??= await _posting.GetBlogEditFormAsync(_viewModel.EntryId);

            if (form is null)
            {
                _viewModel.ErrorMessage = "Форум не дал править эту запись.";
                return;
            }

            _reply.Draft = form.Message;
            _reply.EntryTitle = form.EntryTitle;
            _viewModel.ErrorMessage = string.Empty;

            await Shell.Current.GoToAsync(Routes.ToCompose());
        }
        catch (Exception error)
        {
            _viewModel.ErrorMessage = "Не получилось открыть запись: " + error.Message;
        }
    }

    // Запись собрана нами, и картинки с вложениями в ней зовут приложение
    // выдуманной схемой — так же, как в теме.
    private async void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("cfimage:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            await Shell.Current.GoToAsync(Routes.ToImage(Uri.UnescapeDataString(e.Url["cfimage:".Length..])));
            return;
        }

        if (e.Url.StartsWith("cffile:", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;

            try
            {
                _viewModel.ErrorMessage = "Скачиваем файл…";

                var name = await _downloads.SaveAsync(Uri.UnescapeDataString(e.Url["cffile:".Length..]));

                _viewModel.ErrorMessage = name is null
                    ? "Файл не скачался."
                    : $"Сохранено в «Загрузки»: {name}";
            }
            catch (Exception error)
            {
                _viewModel.ErrorMessage = "Не получилось скачать файл: " + error.Message;
            }

            return;
        }

        // Свой же документ webview объявляет переходом на базовый адрес форума —
        // это не клик человека, трогать нельзя, иначе запись улетает в браузер.
        var ours = e.Url.TrimEnd('/');

        if (ours.Equals(CyberForum.Core.ForumUrls.Base.ToString().TrimEnd('/'), StringComparison.OrdinalIgnoreCase) ||
            e.Url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
            e.Url.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
            e.Url.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // остальные ссылки — наружу, браузером
        if (e.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            await Launcher.OpenAsync(e.Url);
        }
    }
}
