using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.NetEaseMusic.Models;
using Jellyfin.Plugin.NetEaseMusic.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Playlists;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NetEaseMusic.Controllers;

[ApiController]
[Route("NetEaseMusic")]
[Produces("application/json")]
public class NetEasePlaylistController : ControllerBase
{
    private readonly NetEaseScraper _scraper;
    private readonly SongMatcher _matcher;
    private readonly ILibraryManager _libraryManager;
    private readonly IPlaylistManager _playlistManager;
    private readonly IAuthorizationContext _authorizationContext;
    private readonly IUserManager _userManager;
    private readonly ILogger<NetEasePlaylistController> _logger;

    public NetEasePlaylistController(
        NetEaseScraper scraper,
        SongMatcher matcher,
        ILibraryManager libraryManager,
        IPlaylistManager playlistManager,
        IAuthorizationContext authorizationContext,
        IUserManager userManager,
        ILogger<NetEasePlaylistController> logger)
    {
        _scraper = scraper;
        _matcher = matcher;
        _libraryManager = libraryManager;
        _playlistManager = playlistManager;
        _authorizationContext = authorizationContext;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get the current Jellyfin request user.
    /// </summary>
    [HttpGet("CurrentUser")]
    public async Task<ActionResult<CurrentUserResult>> CurrentUser()
    {
        var user = await GetCurrentUser();
        if (user == null)
            return Unauthorized(new { error = "No Jellyfin request user found" });

        return new CurrentUserResult
        {
            UserId = user.Id.ToString(),
            Name = user.Username
        };
    }

    [HttpGet("Imports")]
    public ActionResult<List<CachedImport>> Imports()
    {
        return Plugin.Instance?.Configuration.CachedImports ?? new List<CachedImport>();
    }

    [HttpDelete("Imports/{playlistId}")]
    public IActionResult DeleteImport(string playlistId)
    {
        var plugin = Plugin.Instance;
        if (plugin == null)
            return StatusCode(500, new { error = "Plugin is not ready" });

        plugin.Configuration.CachedImports.RemoveAll(x => x.JellyfinPlaylistId == playlistId);
        plugin.SaveConfiguration();
        return NoContent();
    }

    [HttpPost("Imports/{playlistId}/Refresh")]
    public async Task<ActionResult<ImportResult>> RefreshImport(string playlistId, CancellationToken ct)
    {
        var operationId = NewOperationId();
        var cache = Plugin.Instance?.Configuration.CachedImports
            .FirstOrDefault(x => x.JellyfinPlaylistId == playlistId);
        if (cache == null)
            return NotFound(new { operationId, error = "Import cache not found" });

        var user = await GetCurrentUser();
        if (user == null)
            return StatusCode(401, new { operationId, error = "No Jellyfin request user found" });

        var playlist = _libraryManager.GetItemById(playlistId) as Playlist;
        if (playlist == null)
            return NotFound(new { operationId, error = "Jellyfin playlist not found" });

        var importData = await BuildImportData(cache.NetEaseUrl, operationId, ct);
        if (importData.Result != null)
            return importData.Result;
        var neteaseData = importData.NeteaseData!;

        try
        {
            await SyncPlaylistInternal(playlist, importData.MatchedIds, user);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync Jellyfin playlist");
            return StatusCode(500, new { operationId, error = $"Failed to sync Jellyfin playlist: {ex.Message}" });
        }

        return new ImportResult
        {
            OperationId = operationId,
            PlaylistId = playlistId,
            PlaylistName = playlist.Name,
            MatchedCount = importData.MatchedIds.Count,
            UnmatchedCount = importData.Unmatched.Count,
            TotalCount = neteaseData.Songs.Count,
            UnmatchedSongs = importData.Unmatched
        };
    }

    /// <summary>
    /// Import a NetEase Cloud Music playlist by URL into Jellyfin.
    /// </summary>
    [HttpPost("Import")]
    public async Task<ActionResult<ImportResult>> ImportPlaylist(
        [FromBody] ImportRequest request, CancellationToken ct)
    {
        var operationId = NewOperationId();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["Endpoint"] = "Import"
        });

        _logger.LogInformation("Import request started");

        if (string.IsNullOrWhiteSpace(request.Url) ||
            !request.Url.Contains("music.163.com"))
        {
            _logger.LogWarning("Import request rejected: invalid NetEase playlist URL");
            return BadRequest(new { error = "Invalid NetEase playlist URL" });
        }

        _logger.LogInformation("Import request URL length is {Length}", request.Url.Length);

        var importData = await BuildImportData(request.Url, operationId, ct);
        if (importData.Result != null)
            return importData.Result;
        var neteaseData = importData.NeteaseData!;

        // 3. Get a user for ownership
        var user = await GetCurrentUser();
        if (user == null)
        {
            _logger.LogError("Import failed: no Jellyfin request user found");
            return StatusCode(401, new { operationId, error = "No Jellyfin request user found" });
        }

        // 4. Create the playlist
        var playlistName = request.PlaylistName ?? neteaseData.Name;
        ImportPlaylistResult playlist;
        try
        {
            playlist = await CreatePlaylistInternal(playlistName, importData.MatchedIds, user, request.Public);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Jellyfin playlist");
            return StatusCode(500, new { operationId, error = $"Failed to create Jellyfin playlist: {ex.Message}" });
        }

        _logger.LogInformation("Created playlist '{Name}' ({Id}) with {Count} songs",
            playlistName, playlist.PlaylistId, importData.MatchedIds.Count);

        if (request.SaveCache)
        {
            SaveImportCache(request.Url, playlist.PlaylistId);
        }

        return new ImportResult
        {
            OperationId = operationId,
            PlaylistId = playlist.PlaylistId,
            PlaylistName = playlistName,
            MatchedCount = importData.MatchedIds.Count,
            UnmatchedCount = importData.Unmatched.Count,
            TotalCount = neteaseData.Songs.Count,
            UnmatchedSongs = importData.Unmatched
        };
    }

    private async Task<ImportData> BuildImportData(string url, string operationId, CancellationToken ct)
    {
        // 1. Scrape the playlist
        NetEasePlaylistData neteaseData;
        try
        {
            _logger.LogInformation("Scraping NetEase playlist started");
            neteaseData = await _scraper.ScrapePlaylistAsync(url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape NetEase playlist");
            return new ImportData
            {
                Result = StatusCode(502, new { operationId, error = $"Failed to scrape NetEase playlist: {ex.Message}" })
            };
        }

        _logger.LogInformation("Scraped playlist '{Name}' with {Count} songs",
            neteaseData.Name, neteaseData.Songs.Count);

        using var playlistScope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["PlaylistId"] = neteaseData.PlaylistId,
            ["PlaylistName"] = neteaseData.Name
        });

        // 2. Match songs
        var matchedIds = new List<string>();
        var unmatched = new List<UnmatchedSong>();

        foreach (var song in neteaseData.Songs)
        {
            var match = await _matcher.FindMatchAsync(song, ct);
            if (match != null)
            {
                matchedIds.Add(match);
            }
            else
            {
                unmatched.Add(new UnmatchedSong
                {
                    Name = song.Name,
                    Artist = string.Join(", ", song.Artists),
                    Album = song.Album
                });
            }
        }

        _logger.LogInformation("Matched {Matched}/{Total} songs", matchedIds.Count, neteaseData.Songs.Count);

        return new ImportData
        {
            NeteaseData = neteaseData,
            MatchedIds = matchedIds,
            Unmatched = unmatched
        };
    }

    private async Task<ImportPlaylistResult> CreatePlaylistInternal(string name, List<string> songItemIds, User user, bool isPublic)
    {
        var itemIds = songItemIds
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .ToList();

        var result = await _playlistManager.CreatePlaylist(new PlaylistCreationRequest
        {
            Name = name,
            UserId = user.Id,
            MediaType = MediaType.Audio,
            Public = isPublic
        });

        _logger.LogInformation("Playlist created for user {UserId} with public={Public} and playlistId={PlaylistId}",
            user.Id, isPublic, result.Id);

        var playlistId = Guid.Parse(result.Id);
        if (itemIds.Count > 0)
        {
            await _playlistManager.AddItemToPlaylistAsync(playlistId, itemIds, user.Id);
            _logger.LogInformation("Added {Count} songs to playlist {PlaylistId}", itemIds.Count, result.Id);
        }

        var playlist = _libraryManager.GetItemById(playlistId) as Playlist;

        return new ImportPlaylistResult
        {
            PlaylistId = result.Id,
            Name = playlist?.Name ?? name,
            SongCount = itemIds.Count,
            DateCreated = playlist?.DateCreated ?? DateTime.UtcNow
        };
    }

    private async Task SyncPlaylistInternal(Playlist playlist, List<string> songItemIds, User user)
    {
        var itemIds = songItemIds
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .ToList();
        var playlistId = playlist.Id.ToString();
        var existingIds = playlist.GetLinkedChildrenInfos()
            .Select(x => x.Item1.ItemId?.ToString("N"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .ToList();

        if (existingIds.Count > 0)
        {
            await _playlistManager.RemoveItemFromPlaylistAsync(playlistId, existingIds);
        }

        if (itemIds.Count > 0)
        {
            await _playlistManager.AddItemToPlaylistAsync(playlist.Id, itemIds, user.Id);
        }
    }

    private static void SaveImportCache(string url, string playlistId)
    {
        var plugin = Plugin.Instance;
        if (plugin == null)
            return;

        plugin.Configuration.CachedImports.RemoveAll(x => x.JellyfinPlaylistId == playlistId);
        plugin.Configuration.CachedImports.Add(new CachedImport
        {
            NetEaseUrl = url,
            JellyfinPlaylistId = playlistId
        });
        plugin.SaveConfiguration();
    }

    private async Task<User?> GetCurrentUser()
    {
        var authorization = await _authorizationContext.GetAuthorizationInfo(Request);
        return authorization.User ?? _userManager.GetUserById(authorization.UserId);
    }

    private static string NewOperationId()
    {
        return Guid.NewGuid().ToString("N")[..12];
    }

    private class ImportData
    {
        public NetEasePlaylistData? NeteaseData { get; set; }
        public List<string> MatchedIds { get; set; } = new();
        public List<UnmatchedSong> Unmatched { get; set; } = new();
        public ActionResult<ImportResult>? Result { get; set; }
    }
}
