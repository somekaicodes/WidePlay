using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using Shiny.BluetoothLE;
using Shiny.BluetoothLE.Hosting;
using WidePlay.Models;
// IPeripheral exists in both the central and hosting namespaces — alias the central one.
using CentralPeripheral = Shiny.BluetoothLE.IPeripheral;
using HostingCharacteristic = Shiny.BluetoothLE.Hosting.IGattCharacteristic;

namespace WidePlay.Services;

// Real BLE implementation (Shiny.BluetoothLE).
//
// Host  -> uses the hosting manager to ADVERTISE a Wide Play session and run a GATT
//          server with two characteristics:
//            * Join    (write)  — peers write their device name when joining
//            * Control (notify) — host pushes play/pause/skip/uri commands
// Peer  -> uses the central manager to SCAN for the session UUID, then connects,
//          writes its name to Join, and subscribes to Control to receive commands.
//
// Note: a Spotify URI is too long for the 31-byte BLE advertisement payload, so control
// signals travel over a GATT notification rather than the advertisement itself. Listeners
// therefore hold a lightweight connection while in a session.
public class BleService : IBleService
{
    // Well-known UUIDs identifying a Wide Play session and its characteristics.
    private const string ServiceUuid     = "9f3b7a10-1c2d-4e5f-8a9b-0c1d2e3f4a5b";
    private const string JoinCharUuid    = "9f3b7a11-1c2d-4e5f-8a9b-0c1d2e3f4a5b";
    private const string ControlCharUuid = "9f3b7a12-1c2d-4e5f-8a9b-0c1d2e3f4a5b";

    private readonly IBleManager _central;
    private readonly IBleHostingManager _hosting;

    private readonly Subject<PeerDevice> _sessionDiscovered = new();
    private readonly Subject<string> _commandReceived = new();
    private readonly Subject<PeerDevice> _peerJoined = new();

    // Host: the control characteristic we push commands through.
    private HostingCharacteristic? _controlCharacteristic;

    // Peer: discovered hosts (keyed by id) so SendJoinSignalAsync can reconnect.
    private readonly Dictionary<string, CentralPeripheral> _discovered = new();
    private IDisposable? _scanSub;
    private IDisposable? _notifySub;
    private CentralPeripheral? _connectedHost;

    public IObservable<PeerDevice> SessionDiscovered => _sessionDiscovered;
    public IObservable<string> CommandReceived => _commandReceived;
    public IObservable<PeerDevice> PeerJoined => _peerJoined;

    public BleService(IBleManager central, IBleHostingManager hosting)
    {
        _central = central;
        _hosting = hosting;
    }

    // ---------------- HOST ----------------

    public async Task StartHostingAsync(string sessionName)
    {
        await _hosting.RequestAccess(advertise: true, connect: true);
        _hosting.ClearServices();

        await _hosting.AddService(ServiceUuid, primary: true, sb =>
        {
            // Join characteristic — peers write their device name here when joining.
            sb.AddCharacteristic(JoinCharUuid, cb =>
                cb.SetWrite(req =>
                {
                    var name = Encoding.UTF8.GetString(req.Data);
                    _peerJoined.OnNext(new PeerDevice
                    {
                        Id = req.Peripheral.Uuid,
                        Name = string.IsNullOrWhiteSpace(name) ? "Listener" : name,
                        IsConnected = true,
                    });
                    req.Respond?.Invoke(GattState.Success);
                    return Task.CompletedTask;
                }, WriteOptions.Write));

            // Control characteristic — host notifies subscribers with commands.
            _controlCharacteristic = sb.AddCharacteristic(ControlCharUuid, cb =>
                cb.SetNotification(_ => Task.CompletedTask, NotificationOptions.Notify));
        });

        await _hosting.StartAdvertising(new AdvertisementOptions
        {
            LocalName = sessionName,
            ServiceUuids = [ServiceUuid],
        });
    }

    public async Task SendCommandAsync(string command)
    {
        if (_controlCharacteristic is null) return;

        var centrals = _controlCharacteristic.SubscribedCentrals.ToArray();
        if (centrals.Length == 0) return; // no listeners connected yet

        await _controlCharacteristic.Notify(Encoding.UTF8.GetBytes(command), centrals);
    }

    // ---------------- PEER ----------------

    public Task StartScanningAsync()
    {
        _discovered.Clear();
        _scanSub = _central
            .Scan(new ScanConfig { ServiceUuids = [ServiceUuid] })
            .Subscribe(result =>
            {
                // De-dupe: a host is advertised repeatedly during a scan.
                if (!_discovered.TryAdd(result.Peripheral.Uuid, result.Peripheral))
                    return;

                _sessionDiscovered.OnNext(new PeerDevice
                {
                    Id = result.Peripheral.Uuid,
                    Name = result.AdvertisementData?.LocalName ?? result.Peripheral.Name ?? "Session",
                    SignalStrength = result.Rssi,
                    IsConnected = false,
                });
            });

        return Task.CompletedTask;
    }

    public async Task SendJoinSignalAsync(PeerDevice host, string deviceName)
    {
        if (!_discovered.TryGetValue(host.Id, out var peripheral))
            return;

        _central.StopScan();

        // Connect, announce ourselves on the Join characteristic, then subscribe to Control.
        await peripheral.ConnectAsync(new ConnectionConfig(false), CancellationToken.None, TimeSpan.FromSeconds(10));
        _connectedHost = peripheral;

        await peripheral.WriteCharacteristicAsync(
            ServiceUuid, JoinCharUuid, Encoding.UTF8.GetBytes(deviceName),
            withResponse: true, CancellationToken.None, 5000);

        _notifySub = peripheral
            .NotifyCharacteristic(ServiceUuid, ControlCharUuid, useIndicationsIfAvailable: false)
            .Select(r => r.Event == BleCharacteristicEvent.Notification ? r.Data : null)
            .Where(data => data is { Length: > 0 })
            .Subscribe(data => _commandReceived.OnNext(Encoding.UTF8.GetString(data!)));
    }

    // ---------------- SHARED ----------------

    public Task StopAsync()
    {
        _scanSub?.Dispose();
        _notifySub?.Dispose();
        _scanSub = null;
        _notifySub = null;

        _connectedHost?.CancelConnection();
        _connectedHost = null;

        if (_hosting.IsAdvertising)
            _hosting.StopAdvertising();
        _hosting.ClearServices();
        _controlCharacteristic = null;

        return Task.CompletedTask;
    }
}
