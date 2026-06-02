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
		// Flip to false once you've added your Spotify Client ID in SpotifyConfig.cs
		// (and have a Premium account) to use the real Spotify integration.
		bool useMockSpotify = true;

		builder.Services.AddSingleton<IBleService, MockBleService>();

		if (useMockSpotify)
			builder.Services.AddSingleton<ISpotifyService, MockSpotifyService>();
		else
			builder.Services.AddSingleton<ISpotifyService, SpotifyService>();

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
