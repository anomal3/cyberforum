using Microsoft.Extensions.DependencyInjection;

namespace CyberForum.App;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

#if ANDROID
		// без этого клавиатура наезжает на шторку ответа и закрывает кнопку «Отправить»
		Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.Application
			.SetWindowSoftInputModeAdjust(this, Microsoft.Maui.Controls.PlatformConfiguration.AndroidSpecific.WindowSoftInputModeAdjust.Resize);
#endif
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// сперва своя заставка, она сама переключит окно на вкладки
		return new Window(new Views.StartPage());
	}
}