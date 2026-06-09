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

    // Tracks what was last sent to peers so we only broadcast actual changes.
    private string? _lastBroadcastUri;
    private bool _lastBroadcastIsPlaying;

    // Set to true while a scheduled "startat" is pending so we don't double-broadcast
    // the uri when the host's Spotify starts playing during the countdown.
    public bool ScheduledPlayPending { get; set; }

    // Called by SearchViewModel so the host sees the song title during the countdown.
    public void SetPendingSong(Song song, int countdownSeconds)
    {
        CurrentSong = new Song
        {
            Id = song.Id,
            Title = $"{song.Title} — starting in {countdownSeconds}s",
            Artist = song.Artist,
            AlbumArtUrl = song.AlbumArtUrl,
            DurationMs = song.DurationMs,
            SpotifyUri = song.SpotifyUri,
        };
        IsPlaying = false;
    }

    // Called once the scheduled play actually starts.
    public void ClearPendingSong()
    {
        ScheduledPlayPending = false;
        // Real song title arrives via PlaybackStateChanged; don't reset CurrentSong here.
    }

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

    // Flat properties for song info — avoids MAUI's chained-binding refresh bug.
    public string SongTitle  => CurrentSong?.Title  ?? "Nothing playing";
    public string SongArtist => CurrentSong?.Artist ?? string.Empty;

    // StreamImageSource instead of FileImageSource: MAUI cannot cache stream sources by
    // filename, so the Image control always fetches fresh bytes when the song changes.
    private ImageSource? _albumArt;
    public ImageSource? AlbumArt
    {
        get => _albumArt;
        private set { _albumArt = value; OnPropertyChanged(); }
    }

    private static ImageSource? AlbumArtFrom(string? url) =>
        string.IsNullOrEmpty(url) ? null :
        ImageSource.FromStream(ct => FileSystem.OpenAppPackageFileAsync(url));

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

        // Periodic heartbeat every 8 seconds: resends the current song URI to all peers.
        // Keeps the BLE GATT connection alive and re-syncs any peer that missed a command.
        _ = Task.Run(async () =>
        {
            while (true)
            {
                await Task.Delay(8000);
                if (CurrentSong is { SpotifyUri: var uri } && !ScheduledPlayPending)
                    await _ble.SendCommandAsync($"uri:{uri}");
            }
        });
    }

    // Sends a BLE command to peers only when the song or play/pause state actually changes.
    // Suppressed during a scheduled play so the startat: command isn't overridden.
    private async Task BroadcastStateAsync(PlaybackState state)
    {
        if (ScheduledPlayPending) return;

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
    partial void OnCurrentSongChanged(Song? value)
    {
        OnPropertyChanged(nameof(ProgressValue));
        OnPropertyChanged(nameof(SongTitle));
        OnPropertyChanged(nameof(SongArtist));
        AlbumArt = AlbumArtFrom(value?.AlbumArtUrl);
    }
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
