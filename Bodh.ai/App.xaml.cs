using Microsoft.Extensions.DependencyInjection;

namespace Bodh.ai;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		MainPage = new MainPage();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = base.CreateWindow(activationState);

		window.Destroying += async (sender, e) =>
		{
			await OllamaManager.CoolDownModelAsync();
			OllamaManager.StopServer();
		};

		return window;
	}
}