using Jellyfin.Plugin.NetEaseMusic.Models;

namespace Jellyfin.Plugin.NetEaseMusic.Services;

public interface ISongMatcher
{
    Task<(string ItemId, string UserId)?> FindMatchAsync(NetEaseSongData song, CancellationToken ct = default);
    Task<List<SongSearchResult>> SearchSongsAsync(string query, int maxResults = 20, CancellationToken ct = default);
}
