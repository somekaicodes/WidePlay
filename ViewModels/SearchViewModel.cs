using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WidePlay.Models;
using WidePlay.Services;

namespace WidePlay.ViewModels;

// Drives the Song Search screen — host searches Spotify and picks a track to play.
public partial class SearchViewModel : ObservableObject
{
    // How many seconds ahead both phones schedule the start.
    // 30s gives BLE propagation + Spotify time to buffer on both devices.
    private const int SyncCountdownSeconds = 30;

    private readonly ISpotifyService _spotify;
    private readonly IBleService _ble;
    private readonly PlayerViewModel _player;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public ObservableCollection<Song> SearchResults { get; } = [];

    public SearchViewModel(ISpotifyService spotify, IBleService ble, PlayerViewModel player)
    {
        _spotify = spotify;
        _ble = ble;
        _player = player;
    }

    [RelayCommand]
    private async Task Search()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery)) return;

        IsSearching = true;
        SearchResults.Clear();

        var results = await _spotify.SearchTracksAsync(SearchQuery);
        foreach (var song in results)
            SearchResults.Add(song);

        IsSearching = false;
    }

    // Schedules the song to start on both phones at the same future timestamp.
    // The host broadcasts "startat:{uri}:{timestampMs}" so peers know exactly when to play.
    [RelayCommand]
    private async Task SelectSong(Song song)
    {
        var startAt = DateTimeOffset.UtcNow.AddSeconds(SyncCountdownSeconds);
        var timestampMs = startAt.ToUnixTimeMilliseconds();

        // Suppress auto-broadcast from PlayerViewModel during the countdown
        _player.ScheduledPlayPending = true;

        // Tell all peers when to start
        await _ble.SendCommandAsync($"startat:{song.SpotifyUri}:{timestampMs}");

        await Shell.Current.GoToAsync("//PlayerPage");

        // Host waits the same countdown then plays
        var delay = startAt - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay);

        _player.ScheduledPlayPending = false;
        await _spotify.PlayAsync(song.SpotifyUri);
    }
}
