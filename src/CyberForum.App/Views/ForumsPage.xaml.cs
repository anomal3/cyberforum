using CyberForum.App.ViewModels;

namespace CyberForum.App.Views;

public partial class ForumsPage : ContentPage
{
    private readonly ForumsViewModel _viewModel;

    public ForumsPage(ForumsViewModel viewModel)
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
