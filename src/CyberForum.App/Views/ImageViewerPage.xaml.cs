using CyberForum.App.Services;

namespace CyberForum.App.Views;

/// <summary>
/// Картинка во весь экран: тянется пальцами, двойной тап приближает и возвращает,
/// смахивание вниз закрывает (пока не увеличена — иначе жест нужен самой картинке).
/// </summary>
public partial class ImageViewerPage : ContentPage, IQueryAttributable
{
    private readonly DownloadService _downloads;

    private double _scale = 1;
    private double _startScale = 1;
    private double _shiftX;
    private double _shiftY;
    private string _url = string.Empty;

    public ImageViewerPage(DownloadService downloads)
    {
        InitializeComponent();
        _downloads = downloads;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _url = query.TryGetValue("url", out var value)
            ? Uri.UnescapeDataString(value?.ToString() ?? string.Empty)
            : string.Empty;

        if (string.IsNullOrEmpty(_url))
        {
            Say("Не понял, какую картинку показывать.");
            return;
        }

        Picture.Source = ImageSource.FromUri(new Uri(_url));
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        Picture.PropertyChanged += OnPictureChanged;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        Picture.PropertyChanged -= OnPictureChanged;
    }

    private void OnPictureChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(Image.IsLoading))
        {
            Spinner.IsVisible = Spinner.IsRunning = Picture.IsLoading;
        }
    }

    private void OnPinch(object? sender, PinchGestureUpdatedEventArgs e)
    {
        if (e.Status == GestureStatus.Started)
        {
            _startScale = _scale;
        }

        if (e.Status == GestureStatus.Running)
        {
            _scale = Math.Clamp(_startScale * e.Scale, 1, 6);
            Picture.Scale = _scale;
        }

        if (e.Status is GestureStatus.Completed or GestureStatus.Canceled && _scale <= 1.01)
        {
            Reset();
        }
    }

    private async void OnPan(object? sender, PanUpdatedEventArgs e)
    {
        // Пока картинка не увеличена, движение пальцем — это жест закрытия.
        // Отдельный SwipeGestureRecognizer тут не срабатывает: панорамирование
        // забирает жест себе, поэтому считаем сдвиг сами.
        if (_scale <= 1.01)
        {
            if (e.StatusType == GestureStatus.Running)
            {
                Picture.TranslationY = Math.Max(0, e.TotalY);
                Picture.Opacity = Math.Clamp(1 - Math.Max(0, e.TotalY) / 700, 0.35, 1);
            }

            if (e.StatusType == GestureStatus.Completed)
            {
                if (Picture.TranslationY > 160)
                {
                    await Shell.Current.GoToAsync("..");
                    return;
                }

                Picture.TranslationY = 0;
                Picture.Opacity = 1;
            }

            return;
        }

        if (e.StatusType == GestureStatus.Running)
        {
            Picture.TranslationX = _shiftX + e.TotalX;
            Picture.TranslationY = _shiftY + e.TotalY;
        }

        if (e.StatusType == GestureStatus.Completed)
        {
            _shiftX = Picture.TranslationX;
            _shiftY = Picture.TranslationY;
        }
    }

    private void OnDoubleTap(object? sender, TappedEventArgs e)
    {
        if (_scale > 1.01)
        {
            Reset();
            return;
        }

        _scale = 2.5;
        Picture.Scale = _scale;
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e) =>
        await Shell.Current.GoToAsync("..");

    private async void OnSaveTapped(object? sender, TappedEventArgs e)
    {
        if (string.IsNullOrEmpty(_url))
        {
            return;
        }

        try
        {
            SaveLabel.Text = "Сохраняем…";

            var saved = await _downloads.SaveAsync(_url);

            SaveLabel.Text = "Сохранить";
            Say(saved is null ? "Не получилось сохранить картинку." : $"Сохранено: {saved}");
        }
        catch (Exception error)
        {
            SaveLabel.Text = "Сохранить";
            Say("Не получилось сохранить: " + error.Message);
        }
    }

    private void Reset()
    {
        _scale = 1;
        _shiftX = _shiftY = 0;

        Picture.Scale = 1;
        Picture.TranslationX = 0;
        Picture.TranslationY = 0;
    }

    private async void Say(string message)
    {
        Note.Text = message;
        Note.IsVisible = true;

        await Task.Delay(2500);

        Note.IsVisible = false;
    }
}
