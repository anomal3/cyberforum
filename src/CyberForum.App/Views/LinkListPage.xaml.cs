using CyberForum.App.ViewModels;

namespace CyberForum.App.Views;

public partial class LinkListPage : ContentPage
{
    public LinkListPage(LinkListViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
