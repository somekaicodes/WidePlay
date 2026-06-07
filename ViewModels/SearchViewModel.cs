using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WidePlay.Models;
using WidePlay.Services;

namespace WidePlay.ViewModels;

// Drives the Song Search screen — host searches Spotify and picks a track to play.
public partial class SearchViewModel : ObservableObject
{
    // Countdown in seconds. Short enough to keep BLE connections alive and Spotify active,
    // long enough for the startat: command to reach all peers before playback starts.
    private const int SyncCountdownSeconds = 8;

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

        // Suppress auto-broadcast during the countdown so startat: isn't overridden
        _player.ScheduledPlayPending = true;

        // Show the song immediately on the host screen during the countdown
        _player.SetPendingSong(song, SyncCountdownSeconds);

        // Tell all peers when to start (absolute UTC timestamp)
        await _ble.SendCommandAsync($"startat:{song.SpotifyUri}:{timestampMs}");

        await Shell.Current.GoToAsync("//PlayerPage");

        // Pre-activate Spotify now so it's ready exactly when the countdown ends
        await _spotify.WarmUpAsync();

        // Wait out the remainder of the countdown
        var delay = startAt - DateTimeOffset.UtcNow;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay);

        _player.ClearPendingSong();
        await _spotify.PlayAsync(song.SpotifyUri);
    }
}
