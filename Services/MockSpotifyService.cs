using System.Reactive.Subjects;
using WidePlay.Models;

namespace WidePlay.Services;

// Simulates Spotify without a Premium account or live API calls.
public class MockSpotifyService : ISpotifyService
{
    private readonly Subject<PlaybackState> _playbackStateChanged = new();
    private PlaybackState _state = new();

    public bool IsAuthenticated { get; private set; }
    public IObservable<PlaybackState> PlaybackStateChanged => _playbackStateChanged;

    private static readonly List<Song> FakeCatalog =
    [
        new Song { Id = "1", Title = "Blinding Lights",  Artist = "The Weeknd",    SpotifyUri = "spotify:track:0VjIjW4GlUZAMYd2vXMi3b", DurationMs = 200040 },
        new Song { Id = "2", Title = "Levitating",       Artist = "Dua Lipa",      SpotifyUri = "spotify:track:463CkQjx2Zk1yXoBuierM9", DurationMs = 203064 },
        new Song { Id = "3", Title = "Stay",             Artist = "The Kid LAROI", SpotifyUri = "spotify:track:5HCyWlXZPP0y6Gqq8TgA20", DurationMs = 141005 },
        new Song { Id = "4", Title = "Peaches",          Artist = "Justin Bieber", SpotifyUri = "spotify:track:4iJyoBOLtHqaWYs3vyWVsk", DurationMs = 198082 },
    ];

    public async Task<bool> AuthenticateAsync()
    {
        await Task.Delay(500);
        IsAuthenticated = true;
        return true;
    }

    public async Task<IReadOnlyList<Song>> SearchTracksAsync(string query)
    {
        await Task.Delay(300);
        return FakeCatalog
            .Where(s => s.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || s.Artist.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task PlayAsync(string spotifyUri)
    {
        await Task.Delay(200);
        var song = FakeCatalog.FirstOrDefault(s => s.SpotifyUri == spotifyUri) ?? FakeCatalog[0];
        _state = new PlaybackState { CurrentSong = song, IsPlaying = true, PositionMs = 0 };
        _playbackStateChanged.OnNext(_state);
    }

    public async Task PauseAsync()
    {
        await Task.Delay(100);
        _state.IsPlaying = false;
        _playbackStateChanged.OnNext(_state);
    }

    public async Task ResumeAsync()
    {
        await Task.Delay(100);
        _state.IsPlaying = true;
        _playbackStateChanged.OnNext(_state);
    }

    public async Task SkipPreviousAsync()
    {
        await Task.Delay(200);
        var currentIndex = _state.CurrentSong is null ? 0 : FakeCatalog.IndexOf(_state.CurrentSong);
        var prev = FakeCatalog[(currentIndex - 1 + FakeCatalog.Count) % FakeCatalog.Count];
        _state = new PlaybackState { CurrentSong = prev, IsPlaying = true, PositionMs = 0 };
        _playbackStateChanged.OnNext(_state);
    }

    public async Task SkipNextAsync()
    {
        await Task.Delay(200);
        var currentIndex = _state.CurrentSong is null ? 0 : FakeCatalog.IndexOf(_state.CurrentSong);
        var next = FakeCatalog[(currentIndex + 1) % FakeCatalog.Count];
        _state = new PlaybackState { CurrentSong = next, IsPlaying = true, PositionMs = 0 };
        _playbackStateChanged.OnNext(_state);
    }

    public async Task RefreshPlaybackStateAsync()
    {
        await Task.Delay(100);
        _playbackStateChanged.OnNext(_state);
    }

    public Task<bool> WarmUpAsync() => Task.FromResult(true);
}
