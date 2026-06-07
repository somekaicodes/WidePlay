using Microsoft.Extensions.Logging;
using Shiny;
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
			.UseShiny() // registers Shiny's platform host (required for BLE) — provides Shiny.AndroidPlatform etc.
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// Shiny BLE — central (scanning/connecting) + hosting (advertising/GATT server) roles.
		builder.Services.AddBluetoothLE();
		builder.Services.AddBluetoothLeHosting();

		// Services — singletons so BLE and Spotify state persists for the app lifetime.
		// Flip these to false to use the real integrations:
		//   * useMockSpotify — set false after adding your Client ID in SpotifyConfig.cs (needs Premium)
		//   * useMockBle     — set false to use real BLE (needs two physical devices to test)
		bool useMockSpotify = true;
		bool useMockBle = false;

		if (useMockBle)
			builder.Services.AddSingleton<IBleService, MockBleService>();
		else
			builder.Services.AddSingleton<IBleService, BleService>();

		if (useMockSpotify)
			builder.Services.AddSingleton<ISpotifyService, MockSpotifyService>();
		else
			builder.Services.AddSingleton<ISpotifyService, SpotifyService>();

		// ViewModels — singletons so session state (listener count, current song) survives navigation
		builder.Services.AddSingleton<SessionViewModel>();
		builder.Services.AddSingleton<PlayerViewModel>();
		builder.Services.AddSingleton<PeerViewModel>();
		builder.Services.AddSingleton<SearchViewModel>();

		// Pages — Transient: MAUI Shell creates each page against a per-navigation scoped
		// provider, so a singleton page would outlive (and then access) a disposed scope.
		// The ViewModels stay singletons above, so session state still persists across navigation.
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<PlayerPage>();
		builder.Services.AddTransient<PeerPage>();
		builder.Services.AddTransient<SearchPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
