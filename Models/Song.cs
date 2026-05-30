namespace WidePlay.Models;

public class Song
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string AlbumArtUrl { get; set; } = string.Empty;
    public int DurationMs { get; set; }
    public string SpotifyUri { get; set; } = string.Empty;
}
