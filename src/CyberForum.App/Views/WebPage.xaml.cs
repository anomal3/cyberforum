namespace CyberForum.App.Views;

/// <summary>
/// Страница форума внутри приложения: всё, что мы не переписываем на свой лад
/// (правка профиля, кабинет блога), человек делает тут же, а не в браузере.
/// </summary>
public partial class WebPage : ContentPage, IQueryAttributable
{
    public WebPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    public new string Title { get; private set; } = "Форум";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        Title = Read(query, "title");
        OnPropertyChanged(nameof(Title));

        var url = Read(query, "url");

        if (!string.IsNullOrEmpty(url))
        {
            Page.Source = url;
        }
    }

    private static string Read(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value)
            ? Uri.UnescapeDataString(value?.ToString() ?? string.Empty)
            : string.Empty;
}
