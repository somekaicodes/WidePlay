namespace WidePlay.Models;

public class PlaybackState
{
    public Song? CurrentSong { get; set; }
    public bool IsPlaying { get; set; }
    public int PositionMs { get; set; }
    public string CommanderId { get; set; } = string.Empty; // BLE ID of the session host
}
