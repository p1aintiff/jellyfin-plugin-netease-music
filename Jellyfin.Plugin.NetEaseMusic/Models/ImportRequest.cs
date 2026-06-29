namespace Jellyfin.Plugin.NetEaseMusic.Models;

public class ImportRequest
{
    public string Url { get; set; } = string.Empty;
    public string? PlaylistName { get; set; }
    public bool SkipDuplicates { get; set; } = true;
}

public class AddSongsRequest
{
    public string PlaylistId { get; set; } = string.Empty;
    public List<string> SongItemIds { get; set; } = new();
}

public class CreatePlaylistRequest
{
    public string Name { get; set; } = string.Empty;
    public List<string> SongItemIds { get; set; } = new();
}
