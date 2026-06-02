using SpotifyAPI.Web;

namespace WidePlay.Services;

// Central place for Spotify OAuth settings.
public static class SpotifyConfig
{
    // 1. Create an app at https://developer.spotify.com/dashboard
    // 2. Copy its Client ID here.
    // PKCE does not use a client secret, so this value is safe to ship inside the app.
    public const string ClientId = "YOUR_SPOTIFY_CLIENT_ID";

    // Must be added verbatim as a Redirect URI in the Spotify dashboard AND registered
    // as a URL scheme on each platform (see Platforms/iOS/Info.plist and the Android
    // WebAuthenticatorCallbackActivity).
    public const string RedirectUri = "wideplay://callback";

    // Permissions we request. Playback control requires Spotify Premium.
    public static readonly string[] Scopes =
    [
        SpotifyAPI.Web.Scopes.UserModifyPlaybackState,
        SpotifyAPI.Web.Scopes.UserReadPlaybackState,
        SpotifyAPI.Web.Scopes.UserReadCurrentlyPlaying,
    ];
}
