using CyberForum.App.ViewModels;

namespace CyberForum.App.Views;

public partial class ReputationPage : ContentPage
{
    private readonly ReputationViewModel _viewModel;

    public ReputationPage(ReputationViewModel viewModel)
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
