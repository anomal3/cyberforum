using CyberForum.App.Services;
using CyberForum.Core;

namespace CyberForum.App.Views;

public partial class LoginPage : ContentPage
{
    private readonly SessionService _session;
    private bool _closing;

    public LoginPage(SessionService session)
    {
        InitializeComponent();
        _session = session;
    }

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        // Всё в try: обработчик async void, любая необработанная ошибка внутри него
        // роняет приложение целиком, без единого окна с объяснением.
        try
        {
            Busy(true);

            var (ok, message, state) = await _session.SignInAsync(
                UserBox.Text ?? string.Empty,
                PasswordBox.Text ?? string.Empty);

            PasswordBox.Text = string.Empty;

            if (ok)
            {
                await CloseAsync($"Готово, вошли как {state.UserName}");
                return;
            }

            Say(message ?? "Войти не получилось.");
        }
        catch (Exception error)
        {
            Say("Не получилось войти: " + error.Message);
        }
        finally
        {
            Busy(false);
        }
    }

    // Запасной ход: живая страница форума. Нужен, если он всё-таки спросит капчу.
    private void OnPageLoginTapped(object? sender, TappedEventArgs e)
    {
        Form.IsVisible = false;
        PageLogin.IsVisible = true;

        Login.Source = ForumUrls.Login().ToString();
    }

    // Пока человек ходит по страницам входа и капче — не мешаем. Как только его
    // унесло на обычную страницу форума, пробуем забрать сессию.
    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        try
        {
            if (_closing || IsLoginFlow(e.Url))
            {
                return;
            }

            var state = await _session.AdoptWebViewSessionAsync();

            if (state.IsAuthenticated)
            {
                await CloseAsync($"Готово, вошли как {state.UserName}");
            }
        }
        catch (Exception error)
        {
            Say("Не получилось забрать сессию: " + error.Message);
        }
    }

    private static bool IsLoginFlow(string url) =>
        url.Contains("auth.php", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("login.php", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("register.php", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("recaptcha", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("google.com", StringComparison.OrdinalIgnoreCase);

    private void Busy(bool busy)
    {
        SignInButton.IsEnabled = !busy;
        Spinner.IsVisible = busy;
        Spinner.IsRunning = busy;
    }

    private void Say(string message)
    {
        Hint.Text = message;
        Hint.IsVisible = true;
    }

    private async Task CloseAsync(string message)
    {
        _closing = true;

        await DisplayAlertAsync(string.Empty, message, "Ок");
        await Shell.Current.GoToAsync("..");
    }
}
