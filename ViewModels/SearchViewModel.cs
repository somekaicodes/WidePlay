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

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private bool _isSearching;

    public ObservableCollection<Song> SearchResults { get; } = [];

    public SearchViewModel(ISpotifyService spotify)
    {
        _spotify = spotify;
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

    // Tapping a result plays it on the host; the host's PlayerViewModel sees the
    // playback change and broadcasts the new track to all peers.
    [RelayCommand]
    private async Task SelectSong(Song song)
    {
        await _spotify.PlayAsync(song.SpotifyUri);
        await Shell.Current.GoToAsync("//PlayerPage");
    }
}
