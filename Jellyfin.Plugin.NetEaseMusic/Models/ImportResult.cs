namespace Jellyfin.Plugin.NetEaseMusic.Models;

public class ImportResult
{
    public string OperationId { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public int MatchedCount { get; set; }
    public int UnmatchedCount { get; set; }
    public int TotalCount { get; set; }
    public List<UnmatchedSong> UnmatchedSongs { get; set; } = new();
}

public class UnmatchedSong
{
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
}

public class PlaylistResult
{
    public string OperationId { get; set; } = string.Empty;
    public string PlaylistId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SongCount { get; set; }
    public DateTime DateCreated { get; set; }
}

public class PlaylistListResult
{
    public string OperationId { get; set; } = string.Empty;
    public List<PlaylistResult> Playlists { get; set; } = new();
}

public class DeleteResult
{
    public string OperationId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SongSearchResult
{
    public string OperationId { get; set; } = string.Empty;
    public string ItemId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Artist { get; set; } = string.Empty;
    public string Album { get; set; } = string.Empty;
}

public class CurrentUserResult
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
