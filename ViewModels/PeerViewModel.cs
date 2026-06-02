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
    }

    private async Task HandleCommandAsync(string command)
    {
        if (command == "play")
            await _spotify.ResumeAsync();
        else if (command == "pause")
            await _spotify.PauseAsync();
        else if (command == "skip")
            await _spotify.SkipNextAsync();
        else if (command == "stop")
            await LeaveSession();
        else if (command.StartsWith("uri:"))
            await _spotify.PlayAsync(command["uri:".Length..]);
    }

    [RelayCommand]
    private async Task LeaveSession()
    {
        await _ble.StopAsync();
        await Shell.Current.GoToAsync("//HomePage");
    }
}
