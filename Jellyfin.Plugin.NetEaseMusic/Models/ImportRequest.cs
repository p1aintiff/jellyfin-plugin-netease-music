namespace Jellyfin.Plugin.NetEaseMusic.Models;

public class ImportRequest
{
    public string Url { get; set; } = string.Empty;
    public string? PlaylistName { get; set; }
    public bool Public { get; set; }
}
