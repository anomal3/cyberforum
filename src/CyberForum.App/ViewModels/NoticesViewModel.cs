using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.App.Services;
using CyberForum.Core;
using CyberForum.Core.Models;

namespace CyberForum.App.ViewModels;

public sealed partial class NoticesViewModel(CabinetService cabinet, SessionService session) : BaseViewModel
{
    public ObservableCollection<Notice> Notices { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async _ =>
    {
        Title = "Уведомления";
        Notices.Clear();

        if (!session.Current.IsAuthenticated)
        {
            ErrorMessage = "Уведомления показываются после входа на форум.";
            return;
        }

        var current = await cabinet.RefreshAsync();

        // пустые строки прячем: их там полтора десятка и почти все всегда нули
        foreach (var notice in current.Notices.Where(notice => notice.Count > 0))
        {
            Notices.Add(notice);
        }

        if (Notices.Count == 0)
        {
            ErrorMessage = "Новых уведомлений нет.";
        }
    });

    // Своих экранов под все эти разделы у нас нет, но и выкидывать человека
    // в браузер незачем — показываем страницу форума внутри приложения.
    [RelayCommand]
    private static Task OpenAsync(Notice? notice)
    {
        if (notice is null || string.IsNullOrEmpty(notice.Url) || notice.Url == "#")
        {
            return Task.CompletedTask;
        }

        var address = notice.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(notice.Url)
            : new Uri(ForumUrls.Base, notice.Url);

        return Shell.Current.GoToAsync(Routes.ToWeb(address, notice.Title));
    }
}
