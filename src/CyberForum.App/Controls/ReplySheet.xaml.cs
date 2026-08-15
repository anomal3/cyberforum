namespace CyberForum.App.Controls;

/// <summary>
/// Шторка для быстрого ответа. Сама ничего не отправляет — только собирает текст
/// и сообщает странице, что человек нажал; отправка и все разговоры с форумом
/// остаются там, где для этого есть данные.
/// </summary>
public partial class ReplySheet : ContentView
{
    public ReplySheet() => InitializeComponent();

    /// <summary>Нажали «Отправить».</summary>
    public event EventHandler? Send;

    /// <summary>Нажали «Все кнопки» — надо открыть полный редактор.</summary>
    public event EventHandler? FullEditor;

    /// <summary>Сняли отметку «в ответ такому-то».</summary>
    public event EventHandler? QuoteDropped;

    /// <summary>Шторку закрыли — тапом мимо или кнопкой «назад».</summary>
    public event EventHandler? Closed;

    public bool IsOpen { get; private set; }

    public string Text
    {
        get => Draft.Text ?? string.Empty;
        set => Draft.Text = value;
    }

    public string Placeholder
    {
        get => Draft.Placeholder;
        set => Draft.Placeholder = value;
    }

    public bool ShowFullEditor
    {
        get => FullButton.IsVisible;
        set => FullButton.IsVisible = value;
    }

    public void Open()
    {
        IsOpen = true;
        IsVisible = true;
        Hint.IsVisible = false;

        Draft.Focus();
    }

    public async void Close()
    {
        IsOpen = false;

        /* Одного Unfocus мало: клавиатура на Android остаётся висеть даже когда
           поля на экране уже нет, и убрать её пальцем некуда. Просим систему
           спрятать её явно, и только потом закрываем шторку. */
        try
        {
            await Draft.HideSoftInputAsync(CancellationToken.None);
        }
        catch (Exception)
        {
            // не вышло — шторку всё равно закрываем
        }

        Draft.Unfocus();
        IsVisible = false;

        Closed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Плашка «в ответ такому-то» над полем ввода.</summary>
    public void ShowQuote(string? author)
    {
        QuoteMark.IsVisible = author is not null;
        QuoteText.Text = string.IsNullOrEmpty(author) ? "С цитатой сообщения" : "В ответ: " + author;
    }

    public void Say(string message)
    {
        Hint.Text = message;
        Hint.IsVisible = true;
    }

    public void SetBusy(bool going)
    {
        Busy.IsVisible = Busy.IsRunning = going;
        SendButton.IsEnabled = !going;
        Draft.IsEnabled = !going;
    }

    private void OnShadeTapped(object? sender, TappedEventArgs e) => Close();

    private void OnSendClicked(object? sender, EventArgs e) => Send?.Invoke(this, EventArgs.Empty);

    private void OnFullClicked(object? sender, EventArgs e) => FullEditor?.Invoke(this, EventArgs.Empty);

    private void OnDropQuoteTapped(object? sender, TappedEventArgs e)
    {
        ShowQuote(null);
        QuoteDropped?.Invoke(this, EventArgs.Empty);
    }
}
