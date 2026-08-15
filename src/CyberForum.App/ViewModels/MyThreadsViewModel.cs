using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.App.Services;
using CyberForum.Core;
using CyberForum.Core.Models;

namespace CyberForum.App.ViewModels;

/// <summary>
/// Темы, которые человек начал сам. Отдельного списка у форума для этого нет —
/// он ищет их обычным поиском по имени автора, только с пометкой «начал тему».
/// </summary>
public sealed partial class MyThreadsViewModel(SearchService search, SessionService session) : BaseViewModel
{
    public ObservableCollection<ThreadSummary> Threads { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async token =>
    {
        Title = "Мои темы";
        Threads.Clear();

        var name = session.Current.UserName;

        if (string.IsNullOrEmpty(name))
        {
            ErrorMessage = "Список своих тем форум показывает только вошедшим.";
            return;
        }

        var found = await search.ThreadsByAsync(name, token);

        foreach (var thread in found)
        {
            Threads.Add(thread);
        }

        if (found.Count == 0)
        {
            ErrorMessage = "Форум не нашёл ни одной темы. Может, он просто не в духе — попробуй позже.";
        }
    });

    [RelayCommand]
    private static Task OpenAsync(ThreadSummary? thread) =>
        thread is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToThread(thread.ForumSlug, thread.ThreadId, thread.Title));
}
