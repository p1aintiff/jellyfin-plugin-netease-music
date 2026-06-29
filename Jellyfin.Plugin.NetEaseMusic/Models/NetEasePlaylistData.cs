namespace Jellyfin.Plugin.NetEaseMusic.Models;

public class NetEasePlaylistData
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public long TrackCount { get; set; }
    public List<NetEaseSongData> Songs { get; set; } = new();
}
