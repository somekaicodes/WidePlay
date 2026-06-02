using WidePlay.Models;

namespace WidePlay.Services;

public interface ISpotifyService
{
    // True once the user has completed the OAuth PKCE login flow
    bool IsAuthenticated { get; }

    // Emits whenever playback state changes (current song, is playing, position)
    IObservable<PlaybackState> PlaybackStateChanged { get; }

    // Opens the Spotify OAuth login flow in the browser; returns true on success
    Task<bool> AuthenticateAsync();

    // Search Spotify for tracks matching the query string
    Task<IReadOnlyList<Song>> SearchTracksAsync(string query);

    // Play a specific song by Spotify URI (e.g. "spotify:track:4uLU6hMCjMI75M1A2tKUQC")
    Task PlayAsync(string spotifyUri);

    Task PauseAsync();

    Task ResumeAsync();

    Task SkipPreviousAsync();

    Task SkipNextAsync();

    // Poll current playback state from Spotify Web API and push to PlaybackStateChanged
    Task RefreshPlaybackStateAsync();
}
