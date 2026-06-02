using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WidePlay.Models;
using WidePlay.Services;

namespace WidePlay.ViewModels;

// Drives the Song Search screen — host searches Spotify and picks a track to play.
public partial class SearchViewModel : ObservableObject
{
    private readonly ISpotifyService _spotify;
    private readonly IBleService _ble;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public ObservableCollection<Song> SearchResults { get; } = [];

    public SearchViewModel(ISpotifyService spotify, IBleService ble)
    {
        _spotify = spotify;
        _ble = ble;
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

    // Tapping a result plays it on the host and broadcasts the URI to all peers
    [RelayCommand]
    private async Task SelectSong(Song song)
    {
        await _spotify.PlayAsync(song.SpotifyUri);
        await _ble.SendCommandAsync($"uri:{song.SpotifyUri}");
        await Shell.Current.GoToAsync("//PlayerPage");
    }
}
