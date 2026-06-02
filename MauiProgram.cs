using Microsoft.Extensions.Logging;
using WidePlay.Services;
using WidePlay.ViewModels;
using WidePlay.Views;

namespace WidePlay;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Services — singletons so BLE and Spotify state persists for the app lifetime.
		// Swap Mock* for real implementations once hardware/Spotify auth is ready.
		builder.Services.AddSingleton<IBleService, MockBleService>();
		builder.Services.AddSingleton<ISpotifyService, MockSpotifyService>();

		// ViewModels — singletons so session state (listener count, current song) survives navigation
		builder.Services.AddSingleton<SessionViewModel>();
		builder.Services.AddSingleton<PlayerViewModel>();
		builder.Services.AddSingleton<PeerViewModel>();
		builder.Services.AddSingleton<SearchViewModel>();

		// Pages — resolved by DI so their ViewModels are injected via constructor
		builder.Services.AddSingleton<HomePage>();
		builder.Services.AddSingleton<PlayerPage>();
		builder.Services.AddSingleton<PeerPage>();
		builder.Services.AddSingleton<SearchPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
