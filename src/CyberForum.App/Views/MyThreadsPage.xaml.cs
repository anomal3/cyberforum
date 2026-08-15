using CyberForum.App.ViewModels;

namespace CyberForum.App.Views;

public partial class MyThreadsPage : ContentPage
{
    private readonly MyThreadsViewModel _viewModel;

    public MyThreadsPage(MyThreadsViewModel viewModel)
    {
        InitializeComponent();

        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // список тем меняется редко, но перечитываем каждый раз: он короткий
        _viewModel.LoadCommand.Execute(null);
    }
}
