using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WidePlay.Models;
using WidePlay.Services;

namespace WidePlay.ViewModels;

// Drives the Host / Player screen — playback controls, listener count, session management.
public partial class PlayerViewModel : ObservableObject
{
    private readonly IBleService _ble;
    private readonly ISpotifyService _spotify;

    // Tracks what was last sent to peers, so we only broadcast actual changes.
    private string? _lastBroadcastUri;
    private bool _lastBroadcastIsPlaying;

    [ObservableProperty]
    private Song? _currentSong;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private int _positionMs;

    [ObservableProperty]
    private int _listenerCount;

    // Progress bar value 0.0–1.0, derived from position and song duration
    public double ProgressValue => CurrentSong?.DurationMs > 0
        ? (double)PositionMs / CurrentSong.DurationMs
        : 0;

    // Glyph shown on the play/pause button, driven by IsPlaying
    public string PlayPauseGlyph => IsPlaying ? "⏸" : "▶";

    // "3 listening" / "1 listening" — friendly listener count label
    public string ListenerLabel => $"{ListenerCount} listening";

    public PlayerViewModel(IBleService ble, ISpotifyService spotify)
    {
        _ble = ble;
        _spotify = spotify;

        // The host mirrors its own Spotify playback to peers. Broadcasting from the
        // state stream (rather than from each command) guarantees we always send the
        // track that is actually playing — no UI-thread timing races.
        _spotify.PlaybackStateChanged.Subscribe(state =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentSong = state.CurrentSong;
                IsPlaying = state.IsPlaying;
                PositionMs = state.PositionMs;
                _ = BroadcastStateAsync(state);
            }));

        // When a peer joins, increment the count and immediately send the current song
        // so they don't sit on "Waiting for host..." if a track is already playing.
        _ble.PeerJoined.Subscribe(_ =>
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                ListenerCount++;
                if (CurrentSong is { } song)
                {
                    // Brief delay lets the peer's GATT subscription register before we notify.
                    await Task.Delay(600);
                    await _ble.SendCommandAsync($"uri:{song.SpotifyUri}");
                }
            }));
    }

    // Sends a BLE command to peers only when the song or play/pause state actually changes.
    private async Task BroadcastStateAsync(PlaybackState state)
    {
        try
        {
            if (state.CurrentSong is { } song && song.SpotifyUri != _lastBroadcastUri)
            {
                _lastBroadcastUri = song.SpotifyUri;
                _lastBroadcastIsPlaying = state.IsPlaying;
                await _ble.SendCommandAsync($"uri:{song.SpotifyUri}");
            }
            else if (state.IsPlaying != _lastBroadcastIsPlaying)
            {
                _lastBroadcastIsPlaying = state.IsPlaying;
                await _ble.SendCommandAsync(state.IsPlaying ? "play" : "pause");
            }
        }
        catch
        {
            // A failed BLE send shouldn't crash the host UI; peers resync on the next change.
        }
    }

    // Recompute derived properties whenever their inputs change
    partial void OnPositionMsChanged(int value) => OnPropertyChanged(nameof(ProgressValue));
    partial void OnCurrentSongChanged(Song? value) => OnPropertyChanged(nameof(ProgressValue));
    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayPauseGlyph));
    partial void OnListenerCountChanged(int value) => OnPropertyChanged(nameof(ListenerLabel));

    // Commands just drive Spotify; the resulting state change is what gets broadcast
    // to peers (see BroadcastStateAsync), so the peer always mirrors the real track.
    [RelayCommand]
    private Task PlayPause() => IsPlaying ? _spotify.PauseAsync() : _spotify.ResumeAsync();

    [RelayCommand]
    private Task SkipPrevious() => _spotify.SkipPreviousAsync();

    [RelayCommand]
    private Task SkipNext() => _spotify.SkipNextAsync();

    [RelayCommand]
    private async Task EndSession()
    {
        await _ble.SendCommandAsync("stop");
        await _ble.StopAsync();
        ListenerCount = 0;
        await Shell.Current.GoToAsync("//HomePage");
    }

    [RelayCommand]
    private async Task SearchSong()
    {
        await Shell.Current.GoToAsync("//SearchPage");
    }
}
