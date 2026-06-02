using System.Reactive.Subjects;
using WidePlay.Models;

namespace WidePlay.Services;

// Simulates BLE behaviour in-process so the UI can be built and tested without hardware.
public class MockBleService : IBleService
{
    private readonly Subject<PeerDevice> _sessionDiscovered = new();
    private readonly Subject<string> _commandReceived = new();
    private readonly Subject<PeerDevice> _peerJoined = new();
    private CancellationTokenSource? _scanCts;

    public IObservable<PeerDevice> SessionDiscovered => _sessionDiscovered;
    public IObservable<string> CommandReceived => _commandReceived;
    public IObservable<PeerDevice> PeerJoined => _peerJoined;

    public async Task StartHostingAsync(string sessionName)
    {
        await Task.Delay(100);
        // Simulate two peers joining after the session starts
        _ = Task.Run(async () =>
        {
            await Task.Delay(1000);
            _peerJoined.OnNext(new PeerDevice { Id = "mock-001", Name = "Kai's iPhone", SignalStrength = -55, IsConnected = true });
            await Task.Delay(1500);
            _peerJoined.OnNext(new PeerDevice { Id = "mock-002", Name = "Alex's Galaxy", SignalStrength = -72, IsConnected = true });
        });
    }

    public async Task SendCommandAsync(string command)
    {
        await Task.Delay(50);
        // Echo the command back as if a peer received it — useful for testing peer UI on one device
        _commandReceived.OnNext(command);
    }

    public Task StartScanningAsync()
    {
        _scanCts = new CancellationTokenSource();
        var token = _scanCts.Token;

        // Fire and forget — results arrive via SessionDiscovered observable
        _ = Task.Run(async () =>
        {
            await Task.Delay(800, token);
            if (!token.IsCancellationRequested)
                _sessionDiscovered.OnNext(new PeerDevice { Id = "host-001", Name = "Kai's Session", SignalStrength = -50, IsConnected = false });
        }, token);

        return Task.CompletedTask;
    }

    public async Task SendJoinSignalAsync(PeerDevice host, string deviceName)
    {
        await Task.Delay(200);
        // In the mock, nothing needs to happen — the host mock already fires PeerJoined from StartHostingAsync
    }

    public async Task StopAsync()
    {
        _scanCts?.Cancel();
        await Task.Delay(50);
    }
}
