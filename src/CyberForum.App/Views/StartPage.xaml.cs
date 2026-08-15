using CyberForum.App.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CyberForum.App.Views;

/// <summary>
/// Заставка, которую видно вместо системной: та на Android рисуется крохотной
/// и её размер никак не задать. Знак чуть подрастает, слова проявляются,
/// а через мгновение окно уезжает на обычные вкладки.
/// </summary>
public partial class StartPage : ContentPage
{
    private bool _left;

    public StartPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        if (_left)
        {
            return;
        }

        _left = true;

        // Пока крутится заставка, поднимаем прошлый вход из хранилища: иначе
        // приложение каждый раз запускается гостем, хотя сессия сохранена.
        if (IPlatformApplication.Current?.Services.GetService<SessionService>() is { } session)
        {
            _ = session.RestoreAsync();
        }

        Mark.Opacity = 0;
        Mark.Scale = 0.88;
        Words.Opacity = 0;
        Words.TranslationY = 14;

        await Task.WhenAll(
            Mark.FadeToAsync(1, 260, Easing.CubicOut),
            Mark.ScaleToAsync(1, 420, Easing.CubicOut));

        await Task.WhenAll(
            Words.FadeToAsync(1, 220, Easing.CubicOut),
            Words.TranslateToAsync(0, 0, 260, Easing.CubicOut));

        await Task.Delay(420);
        await Root.FadeToAsync(0, 180, Easing.CubicIn);

        if (Application.Current?.Windows.Count > 0)
        {
            Application.Current.Windows[0].Page = new AppShell();
        }
    }
}
