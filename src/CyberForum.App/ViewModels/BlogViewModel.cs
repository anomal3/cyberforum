using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.Core;
using CyberForum.Core.Models;

namespace CyberForum.App.ViewModels;

/// <summary>
/// Список записей блога. Блоги форум отдаёт обычным запросом, поэтому читаем их
/// внутри приложения, а не отправляем человека в браузер.
/// </summary>
public sealed partial class BlogViewModel(ForumClient client) : BaseViewModel, IQueryAttributable
{
    public ObservableCollection<BlogPost> Posts { get; } = [];

    [ObservableProperty]
    public partial int UserId { get; set; }

    public async void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        UserId = Read(query, "user");
        Title = Read(query, "title") is var name && name > 0 ? "Блог" : "Блог";

        await LoadCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async token =>
    {
        if (UserId <= 0)
        {
            ErrorMessage = "Не понял, чей блог открывать.";
            return;
        }

        Posts.Clear();

        foreach (var post in await client.GetBlogAsync(UserId, token))
        {
            Posts.Add(post);
        }

        if (Posts.Count == 0)
        {
            ErrorMessage = "В блоге пока нет записей.";
        }
    });

    [RelayCommand]
    private static Task OpenAsync(BlogPost? post) =>
        post is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToBlogEntry(post.UserId, post.EntryId, post.Title));

    private static int Read(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var number) ? number : 0;
}
