using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.Core;
using CyberForum.Core.Models;

namespace CyberForum.App.ViewModels;

public sealed partial class SearchViewModel(SearchService search) : BaseViewModel
{
    public ObservableCollection<ThreadSummary> Results { get; } = [];

    [ObservableProperty]
    public partial string Query { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Searched { get; set; }

    [RelayCommand]
    private Task FindAsync() => RunAsync(async token =>
    {
        Title = "Поиск";
        Results.Clear();

        if (string.IsNullOrWhiteSpace(Query))
        {
            return;
        }

        var found = await search.SearchAsync(Query, titlesOnly: true, token);

        foreach (var thread in found)
        {
            Results.Add(thread);
        }

        Searched = true;

        if (found.Count == 0)
        {
            ErrorMessage = "Ничего не нашлось. Попробуй другие слова.";
        }
    });

    [RelayCommand]
    private static Task OpenAsync(ThreadSummary? thread) =>
        thread is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToThread(thread.ForumSlug, thread.ThreadId, thread.Title));
}
