using CyberForum.App.ViewModels;

namespace CyberForum.App.Views;

public partial class BlogPage : ContentPage
{
    public BlogPage(BlogViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
