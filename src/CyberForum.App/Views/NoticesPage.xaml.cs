using CyberForum.App.ViewModels;

namespace CyberForum.App.Views;

public partial class NoticesPage : ContentPage
{
    private readonly NoticesViewModel _viewModel;

    public NoticesPage(NoticesViewModel viewModel)
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
