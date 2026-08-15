using CommunityToolkit.Mvvm.ComponentModel;
using CyberForum.Core.Http;

namespace CyberForum.App.ViewModels;

public abstract partial class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // Одна воронка для всех загрузок: не даём запускать две сразу и по-человечески
    // объясняем, что случилось, вместо голого исключения.
    protected async Task RunAsync(Func<CancellationToken, Task> work, CancellationToken token = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;

        try
        {
            await work(token);
        }
        catch (ForumBlockedException)
        {
            ErrorMessage = "Форум не пустил: он так отвечает, когда запрос похож на робота. " +
                           "Попробуй ещё раз чуть позже или из мобильного интернета.";
        }
        catch (ForumCaptchaException)
        {
            ErrorMessage = "Форум просит пройти проверку «я не робот». Гостям он поиск не отдаёт — " +
                           "войди в свой аккаунт на вкладке «Профиль», и поиск заработает.";
        }
        catch (ForumUnavailableException)
        {
            ErrorMessage = "Форум не отвечает. Проверь связь и попробуй снова.";
        }
        catch (OperationCanceledException)
        {
            // ушли со страницы — молча выходим
        }
        catch (Exception error)
        {
            System.Diagnostics.Debug.WriteLine($"[CyberForum] {error}");
            ErrorMessage = $"Что-то пошло не так: {error.Message}";
        }
        finally
        {
            IsBusy = false;
            IsRefreshing = false;
        }
    }
}
