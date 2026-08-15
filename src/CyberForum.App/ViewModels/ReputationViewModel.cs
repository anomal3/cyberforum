using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.App.Services;
using CyberForum.Core.Models;

namespace CyberForum.App.ViewModels;

public sealed partial class ReputationViewModel(CabinetService cabinet) : BaseViewModel
{
    public ObservableCollection<ReputationNote> Notes { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async _ =>
    {
        if (!cabinet.Loaded)
        {
            await cabinet.RefreshAsync();
        }

        Title = $"Отзывы · {cabinet.Current.ReputationTotal} баллов";

        Notes.Clear();
        foreach (var note in cabinet.Current.Reputation)
        {
            Notes.Add(note);
        }
    });

    [RelayCommand]
    private static Task OpenAsync(ReputationNote? note) =>
        note is null || note.ThreadId <= 0
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToThread(note.ForumSlug, note.ThreadId, note.ThreadTitle));
}
