namespace Jellyfin.Plugin.NetEaseMusic.Models;

public class NetEaseSongData
{
    public string Name { get; set; } = string.Empty;
    public List<string> Artists { get; set; } = new();
    public string Album { get; set; } = string.Empty;
    public long DurationMs { get; set; }
}
