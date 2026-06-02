using Android.App;
using Android.Content;
using Android.Content.PM;

namespace WidePlay.Platforms.Android;

// Receives the Spotify OAuth redirect (wideplay://callback) and hands it back to
// MAUI's WebAuthenticator. The intent filter must match SpotifyConfig.RedirectUri.
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "wideplay",
    DataHost = "callback")]
public class WebAuthenticatorCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
