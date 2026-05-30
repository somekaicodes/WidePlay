namespace WidePlay.Models;

public class PeerDevice
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SignalStrength { get; set; }   // RSSI in dBm
    public bool IsConnected { get; set; }
}
