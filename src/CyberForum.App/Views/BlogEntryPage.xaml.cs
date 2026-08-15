using CyberForum.App.Services;
using CyberForum.App.ViewModels;

namespace CyberForum.App.Views;

public partial class BlogEntryPage : ContentPage
{
    private readonly BlogEntryViewModel _viewModel;
    private readonly DownloadService _downloads;

    public BlogEntryPage(BlogEntryViewModel viewModel, DownloadService downloads)
    {
        InitializeComponent();

        BindingContext = _viewModel = viewModel;
        _downloads = downloads;
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
