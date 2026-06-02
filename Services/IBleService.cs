using WidePlay.Models;

namespace WidePlay.Services;

// BLE architecture: Option B with one-shot GATT join signal.
// Host broadcasts control signals via BLE advertising; peers listen passively.
// When a peer joins, they briefly connect to the host's GATT server to send
// a join signal, then disconnect and return to passive listening.
public interface IBleService
{
    // Emits nearby BLE sessions found during a scan (host name + BLE ID)
    IObservable<PeerDevice> SessionDiscovered { get; }

    // Emits BLE commands received by a peer (e.g. "play", "pause", "skip", "uri:<spotifyUri>")
    IObservable<string> CommandReceived { get; }

    // Emits when a peer sends a one-shot join signal to the host's GATT server
    IObservable<PeerDevice> PeerJoined { get; }

    // HOST: start advertising control signals and open the GATT join characteristic
    Task StartHostingAsync(string sessionName);

    // HOST: broadcast a command to all nearby peers via BLE advertisement payload
    Task SendCommandAsync(string command);

    // PEER: scan for nearby hosted sessions
    Task StartScanningAsync();

    // PEER: send a one-shot join signal to the host via GATT, then disconnect
    Task SendJoinSignalAsync(PeerDevice host, string deviceName);

    // Stop all BLE activity (advertising, GATT server, or scanning)
    Task StopAsync();
}
