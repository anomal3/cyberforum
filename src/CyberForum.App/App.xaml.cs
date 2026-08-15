using Microsoft.Extensions.DependencyInjection;

namespace CyberForum.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// сперва своя заставка, она сама переключит окно на вкладки
		return new Window(new Views.StartPage());
	}
}