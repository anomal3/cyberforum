using CyberForum.App.ViewModels;

namespace CyberForum.App.Views;

public partial class ThreadListPage : ContentPage
{
    private readonly ThreadListViewModel _viewModel;

    public ThreadListPage(ThreadListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
