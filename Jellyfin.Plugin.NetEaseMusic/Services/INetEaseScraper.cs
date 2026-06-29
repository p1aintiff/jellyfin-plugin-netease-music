using Jellyfin.Plugin.NetEaseMusic.Models;

namespace Jellyfin.Plugin.NetEaseMusic.Services;

public interface INetEaseScraper
{
    Task<NetEasePlaylistData> ScrapePlaylistAsync(string playlistUrl, CancellationToken ct = default);
}
