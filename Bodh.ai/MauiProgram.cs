using Microsoft.Extensions.Logging;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Indiko.Maui.Controls.Markdown;
using Microsoft.Extensions.DependencyInjection;

namespace Bodh.ai;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		var assembly = Assembly.GetExecutingAssembly();
		var resourceName = $"{assembly.GetName().Name}.appsettings.json";
		using var stream = assembly.GetManifestResourceStream(resourceName);

		if (stream != null)
		{
			var config = new ConfigurationBuilder()
				.AddJsonStream(stream)
				.Build();
			builder.Configuration.AddConfiguration(config);
		}

		// Developer code: these registrations keep the UI free of direct concrete-object creation and make services easier to test.
		// Developer code: register the pre-created singleton instance so DI never tries to
		// construct LoggerService through its intentionally private constructor.
		builder.Services.AddSingleton<ILoggerService>(_ => LoggerService.Instance);
		builder.Services.AddSingleton<IConfigurationService, ConfigurationService>();
		builder.Services.AddSingleton<IOllamaService, OllamaServiceClass>();
		builder.Services.AddSingleton<IMySqlVectorRepository>(sp =>
			new MySqlVectorRepository(
				sp.GetRequiredService<IConfigurationService>().GetConnectionString(),
				sp.GetRequiredService<ILoggerService>()));

		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			})
			.UseMarkdownView();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
