using System.Reactive.Subjects;
using Microsoft.Maui.Authentication;
using Microsoft.Maui.Storage;
using SpotifyAPI.Web;
using WidePlay.Models;

namespace WidePlay.Services;

// Real Spotify integration: OAuth (PKCE) login, track search, and playback control
// via the Spotify Web API. Playback control requires a Premium account with an
// active Spotify device (e.g. the Spotify app running on the same phone).
public class SpotifyService : ISpotifyService
{
    // Refresh token is stored in the OS secure store so the user stays logged in across launches.
    private const string RefreshTokenKey = "spotify_refresh_token";

    private readonly Subject<PlaybackState> _playbackStateChanged = new();
    private SpotifyClient? _client;

    public bool IsAuthenticated => _client is not null;
    public IObservable<PlaybackState> PlaybackStateChanged => _playbackStateChanged;

    public async Task<bool> AuthenticateAsync()
    {
        // Fast path: if we already have a refresh token, skip the browser and silently re-auth.
        if (await TryRestoreSessionAsync())
            return true;

        try
        {
            // 1. Generate PKCE verifier/challenge pair
            var (verifier, challenge) = PKCEUtil.GenerateCodes();

            // 2. Build the Spotify authorize URL
            var loginRequest = new LoginRequest(
                new Uri(SpotifyConfig.RedirectUri),
                SpotifyConfig.ClientId,
                LoginRequest.ResponseType.Code)
            {
                CodeChallengeMethod = "S256",
                CodeChallenge = challenge,
                Scope = SpotifyConfig.Scopes,
            };

            // 3. Open the system browser and wait for the redirect back to our custom scheme
            var authResult = await WebAuthenticator.Default.AuthenticateAsync(
                loginRequest.ToUri(),
                new Uri(SpotifyConfig.RedirectUri));

            if (!authResult.Properties.TryGetValue("code", out var code))
                return false;

            // 4. Exchange the authorization code for access + refresh tokens
            var tokens = await new OAuthClient().RequestToken(
                new PKCETokenRequest(SpotifyConfig.ClientId, code, new Uri(SpotifyConfig.RedirectUri), verifier));

            await SecureStorage.Default.SetAsync(RefreshTokenKey, tokens.RefreshToken);
            CreateClient(tokens);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Tries to rebuild a client from a stored refresh token without prompting the user.
    private async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var refreshToken = await SecureStorage.Default.GetAsync(RefreshTokenKey);
            if (string.IsNullOrEmpty(refreshToken))
                return false;

            var tokens = await new OAuthClient().RequestToken(
                new PKCETokenRefreshRequest(SpotifyConfig.ClientId, refreshToken));

            CreateClient(tokens);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Wraps the tokens in a self-refreshing authenticator and persists rotated refresh tokens.
    private void CreateClient(PKCETokenResponse tokens)
    {
        var authenticator = new PKCEAuthenticator(SpotifyConfig.ClientId, tokens);
        authenticator.TokenRefreshed += (_, token) =>
            _ = SecureStorage.Default.SetAsync(RefreshTokenKey, token.RefreshToken);

        var config = SpotifyClientConfig.CreateDefault().WithAuthenticator(authenticator);
        _client = new SpotifyClient(config);
    }

    public async Task<IReadOnlyList<Song>> SearchTracksAsync(string query)
    {
        if (_client is null) return [];

        var response = await _client.Search.Item(new SearchRequest(SearchRequest.Types.Track, query));
        return response.Tracks.Items?.Select(MapTrack).ToList() ?? [];
    }

    public async Task PlayAsync(string spotifyUri)
    {
        if (_client is null) return;
        await SafePlayerCall(async () =>
        {
            await _client.Player.ResumePlayback(new PlayerResumePlaybackRequest { Uris = [spotifyUri] });
            await RefreshPlaybackStateAsync();
        });
    }

    public async Task PauseAsync()
    {
        if (_client is null) return;
        await SafePlayerCall(async () =>
        {
            await _client.Player.PausePlayback();
            await RefreshPlaybackStateAsync();
        });
    }

    public async Task ResumeAsync()
    {
        if (_client is null) return;
        await SafePlayerCall(async () =>
        {
            await _client.Player.ResumePlayback();
            await RefreshPlaybackStateAsync();
        });
    }

    public async Task SkipPreviousAsync()
    {
        if (_client is null) return;
        await SafePlayerCall(async () =>
        {
            await _client.Player.SkipPrevious();
            await RefreshPlaybackStateAsync();
        });
    }

    public async Task SkipNextAsync()
    {
        if (_client is null) return;
        await SafePlayerCall(async () =>
        {
            await _client.Player.SkipNext();
            await RefreshPlaybackStateAsync();
        });
    }

    // Wraps Spotify player calls so an "No active device" error shows a friendly alert
    // rather than crashing the app. The Spotify app must be open and active on the device.
    private static async Task SafePlayerCall(Func<Task> call)
    {
        try
        {
            await call();
        }
        catch (APIException ex) when (ex.Message.Contains("No active device"))
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Application.Current?.Windows[0].Page?.DisplayAlertAsync(
                    "Open Spotify",
                    "Please open the Spotify app and play or pause a track first, then try again.",
                    "OK") ?? Task.CompletedTask);
        }
        catch
        {
            // Swallow other transient API errors (rate limits, network blips) silently.
        }
    }

    public async Task RefreshPlaybackStateAsync()
    {
        if (_client is null) return;

        var playback = await _client.Player.GetCurrentPlayback();
        if (playback?.Item is not FullTrack track) return;

        _playbackStateChanged.OnNext(new PlaybackState
        {
            CurrentSong = MapTrack(track),
            IsPlaying = playback.IsPlaying,
            PositionMs = playback.ProgressMs,
        });
    }

    // Maps a Spotify API track onto our own Song model so the rest of the app
    // never depends on SpotifyAPI.Web types.
    private static Song MapTrack(FullTrack t) => new()
    {
        Id = t.Id,
        Title = t.Name,
        Artist = string.Join(", ", t.Artists.Select(a => a.Name)),
        AlbumArtUrl = t.Album.Images.FirstOrDefault()?.Url ?? string.Empty,
        DurationMs = t.DurationMs,
        SpotifyUri = t.Uri,
    };
}
