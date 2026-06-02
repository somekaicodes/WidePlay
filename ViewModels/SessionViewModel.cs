using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using WidePlay.Models;
using WidePlay.Services;

namespace WidePlay.ViewModels;

// Drives the Home screen — Spotify login, hosting a session, and joining one nearby.
public partial class SessionViewModel : ObservableObject
{
    private readonly IBleService _ble;
    private readonly ISpotifyService _spotify;

    [ObservableProperty]
    private bool _isSpotifyConnected;

    [ObservableProperty]
    private bool _isScanning;

    // Populated in real-time as nearby BLE sessions are discovered
    public ObservableCollection<PeerDevice> DiscoveredSessions { get; } = [];

    public SessionViewModel(IBleService ble, ISpotifyService spotify)
    {
        _ble = ble;
        _spotify = spotify;
        IsSpotifyConnected = spotify.IsAuthenticated;

        // Subscribe to the BLE scan stream; marshal to the UI thread before touching the ObservableCollection
        _ble.SessionDiscovered.Subscribe(session =>
            MainThread.BeginInvokeOnMainThread(() => DiscoveredSessions.Add(session)));
    }

    [RelayCommand]
    private async Task ConnectSpotify()
    {
        IsSpotifyConnected = await _spotify.AuthenticateAsync();
    }

    [RelayCommand]
    private async Task StartScanning()
    {
        IsScanning = true;
        DiscoveredSessions.Clear();
        await _ble.StartScanningAsync();
        IsScanning = false;
    }

    [RelayCommand]
    private async Task HostSession()
    {
        await _ble.StartHostingAsync(DeviceInfo.Current.Name);
        await Shell.Current.GoToAsync("//PlayerPage");
    }

    // Called when the user taps a session in the nearby list
    [RelayCommand]
    private async Task JoinSession(PeerDevice host)
    {
        await _ble.SendJoinSignalAsync(host, DeviceInfo.Current.Name);
        await Shell.Current.GoToAsync($"//PeerPage?hostName={Uri.EscapeDataString(host.Name)}");
    }
}
