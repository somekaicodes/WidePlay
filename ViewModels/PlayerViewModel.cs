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

        _spotify.PlaybackStateChanged.Subscribe(state =>
            MainThread.BeginInvokeOnMainThread(() =>
            {
                CurrentSong = state.CurrentSong;
                IsPlaying = state.IsPlaying;
                PositionMs = state.PositionMs;
            }));

        // Each join signal increments the listener count
        _ble.PeerJoined.Subscribe(_ =>
            MainThread.BeginInvokeOnMainThread(() => ListenerCount++));
    }

    // Recompute derived properties whenever their inputs change
    partial void OnPositionMsChanged(int value) => OnPropertyChanged(nameof(ProgressValue));
    partial void OnCurrentSongChanged(Song? value) => OnPropertyChanged(nameof(ProgressValue));
    partial void OnIsPlayingChanged(bool value) => OnPropertyChanged(nameof(PlayPauseGlyph));
    partial void OnListenerCountChanged(int value) => OnPropertyChanged(nameof(ListenerLabel));

    [RelayCommand]
    private async Task PlayPause()
    {
        if (IsPlaying)
        {
            await _spotify.PauseAsync();
            await _ble.SendCommandAsync("pause");
        }
        else
        {
            await _spotify.ResumeAsync();
            await _ble.SendCommandAsync("play");
        }
    }

    [RelayCommand]
    private async Task SkipPrevious()
    {
        await _spotify.SkipPreviousAsync();
        if (CurrentSong is not null)
            await _ble.SendCommandAsync($"uri:{CurrentSong.SpotifyUri}");
    }

    [RelayCommand]
    private async Task SkipNext()
    {
        await _spotify.SkipNextAsync();
        if (CurrentSong is not null)
            await _ble.SendCommandAsync($"uri:{CurrentSong.SpotifyUri}");
    }

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
