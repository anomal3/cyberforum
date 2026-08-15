using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.Core;
using CyberForum.Core.Rendering;

namespace CyberForum.App.ViewModels;

/// <summary>
/// Одна запись блога, собранная в наш читаемый документ.
/// </summary>
public sealed partial class BlogEntryViewModel(
    ForumClient client,
    BlogDocumentBuilder builder,
    CyberForum.App.Services.SessionService session)
    : BaseViewModel, IQueryAttributable
{
    [ObservableProperty]
    public partial HtmlWebViewSource? Document { get; set; }

    [ObservableProperty]
    public partial int UserId { get; set; }

    [ObservableProperty]
    public partial int EntryId { get; set; }

    /// <summary>Своя запись — её можно переписать, чужую только читать.</summary>
    [ObservableProperty]
    public partial bool IsMine { get; set; }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        UserId = Number(query, "user");
        EntryId = Number(query, "entry");
        Title = Text(query, "title");

        await LoadAsync();
    }

    // после комментария или правки запись надо перечитать, минуя кэш
    [RelayCommand]
    private Task ReloadAsync() => LoadAsync(fresh: true);

    private Task LoadAsync(bool fresh = false) => RunAsync(async token =>
    {
        if (UserId <= 0 || EntryId <= 0)
        {
            ErrorMessage = "Не понял, какую запись открывать.";
            return;
        }

        // своя запись — её можно переписать; сравниваем, когда номер уже известен
        IsMine = session.Current.UserId is { } me && me == UserId;

        var post = await client.GetBlogEntryAsync(UserId, EntryId, fresh, token);

        if (post is null)
        {
            ErrorMessage = "Запись не открылась. Попробуй ещё раз.";
            return;
        }

        if (!string.IsNullOrEmpty(post.Title))
        {
            Title = post.Title;
        }

        Document = new HtmlWebViewSource
        {
            Html = builder.Build(post, ThreadStyles.Default),
            BaseUrl = ForumUrls.Base.ToString(),
        };
    });

    private static int Number(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var number) ? number : 0;

    private static string Text(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value)
            ? Uri.UnescapeDataString(value?.ToString() ?? string.Empty)
            : string.Empty;
}
