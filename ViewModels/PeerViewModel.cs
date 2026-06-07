using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WidePlay.Models;
using WidePlay.Services;

namespace WidePlay.ViewModels;

// Drives the Peer / Now Playing screen — read-only playback view for listeners.
// IQueryAttributable lets MAUI Shell pass the host name as a navigation parameter.
public partial class PeerViewModel : ObservableObject, IQueryAttributable
{
    private readonly IBleService _ble;
    private readonly ISpotifyService _spotify;

    [ObservableProperty]
    private Song? _currentSong;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _hostName = string.Empty;

    public PeerViewModel(IBleService ble, ISpotifyService spotify)
    {
        _ble = ble;
        _spotify = spotify;

        // Listen for BLE commands from the host and act on them via Spotify
        _ble.CommandReceived.Subscribe(command =>
            MainThread.BeginInvokeOnMainThread(() => _ = HandleCommandAsync(command)));

        // Keep the Now Playing UI in sync with Spotify playback state
        _spotify.PlaybackStateChanged.Subscribe(state =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentSong = state.CurrentSong;
                IsPlaying = state.IsPlaying;
            }));
    }

    // Called by Shell navigation when arriving from SessionViewModel.JoinSession
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("hostName", out var name))
            HostName = Uri.UnescapeDataString(name?.ToString() ?? string.Empty);

        // Reset playback state for each new session (ViewModel is a singleton so
        // stale state from a previous session would otherwise linger).
        CurrentSong = null;
        IsPlaying = false;
    }

    private async Task HandleCommandAsync(string command)
    {
        if (command == "play")
        {
            IsPlaying = true;
            await _spotify.ResumeAsync();
        }
        else if (command == "pause")
        {
            IsPlaying = false;
            await _spotify.PauseAsync();
        }
        else if (command == "skip")
            await _spotify.SkipNextAsync();
        else if (command == "stop")
            await LeaveSession();
        else if (command.StartsWith("uri:"))
            await PlayFromUri(command["uri:".Length..]);
        else if (command.StartsWith("startat:"))
            await HandleScheduledPlay(command);
    }

    private async Task PlayFromUri(string uri)
    {
        IsPlaying = true;
        // Show a placeholder immediately; real title arrives via PlaybackStateChanged.
        CurrentSong = new Song { SpotifyUri = uri, Title = "Loading…" };
        await _spotify.PlayAsync(uri);
    }

    // "startat:{spotifyUri}:{unixTimestampMs}" — both phones wait until the same
    // moment then start playing, achieving near-perfect sync without a live clock signal.
    private async Task HandleScheduledPlay(string command)
    {
        var parts = command.Split(':', 3); // ["startat", "spotify", "track:xxx:timestamp"]
        // command format: "startat:{spotifyUri}:{timestampMs}"
        // spotifyUri itself contains colons so we split from the right
        var lastColon = command.LastIndexOf(':');
        if (lastColon < 0) return;

        var timestampStr = command[(lastColon + 1)..];
        var spotifyUri = command["startat:".Length..lastColon];

        if (!long.TryParse(timestampStr, out var timestampMs)) return;

        var startAt = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
        var delay = startAt - DateTimeOffset.UtcNow;

        // Show countdown immediately so the listener knows when music starts.
        CurrentSong = new Song { SpotifyUri = spotifyUri, Title = $"Starting in {(int)delay.TotalSeconds}s…" };
        IsPlaying = false;

        if (delay > TimeSpan.Zero)
            await Task.Delay(delay);

        await PlayFromUri(spotifyUri);
    }

    [RelayCommand]
    private async Task LeaveSession()
    {
        await _ble.StopAsync();
        await Shell.Current.GoToAsync("//HomePage");
    }
}
