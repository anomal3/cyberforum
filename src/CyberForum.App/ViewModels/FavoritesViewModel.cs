using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using CyberForum.Core.Storage;

namespace CyberForum.App.ViewModels;

public sealed partial class FavoritesViewModel(CacheStore cache) : BaseViewModel
{
    public ObservableCollection<ThreadReadState> Rows { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async _ =>
    {
        Title = "Избранное";
        Rows.Clear();

        foreach (var state in await cache.GetFavoritesAsync())
        {
            Rows.Add(state);
        }
    });

    [RelayCommand]
    private static Task OpenAsync(ThreadReadState? state) =>
        state is null
            ? Task.CompletedTask
            : Shell.Current.GoToAsync(Routes.ToThread(state.Slug, state.ThreadId, state.Title, state.Page));

    // свайпнул влево, ткнул в корзину — тема ушла из избранного
    [RelayCommand]
    private async Task RemoveAsync(ThreadReadState? state)
    {
        if (state is null)
        {
            return;
        }

        await cache.ToggleFavoriteAsync(state);
        Rows.Remove(state);
    }
}
