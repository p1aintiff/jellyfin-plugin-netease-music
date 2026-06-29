using Jellyfin.Data.Entities;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.NetEaseMusic.Models;
using Jellyfin.Plugin.NetEaseMusic.Services;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
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
    private readonly INetEaseScraper _scraper;
    private readonly ISongMatcher _matcher;
    private readonly ILibraryManager _libraryManager;
    private readonly IPlaylistManager _playlistManager;
    private readonly IAuthorizationContext _authorizationContext;
    private readonly IUserManager _userManager;
    private readonly ILogger<NetEasePlaylistController> _logger;

    public NetEasePlaylistController(
        INetEaseScraper scraper,
        ISongMatcher matcher,
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

        // 1. Scrape the playlist
        NetEasePlaylistData neteaseData;
        try
        {
            _logger.LogInformation("Scraping NetEase playlist started");
            neteaseData = await _scraper.ScrapePlaylistAsync(request.Url, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape NetEase playlist");
            return StatusCode(502, new { operationId, error = $"Failed to scrape NetEase playlist: {ex.Message}" });
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
            if (match.HasValue)
            {
                matchedIds.Add(match.Value.ItemId);
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

        // 3. Get a user for ownership
        var user = await GetCurrentUser();
        if (user == null)
        {
            _logger.LogError("Import failed: no Jellyfin request user found");
            return StatusCode(401, new { operationId, error = "No Jellyfin request user found" });
        }

        // 4. Create the playlist
        var playlistName = request.PlaylistName ?? neteaseData.Name;
        PlaylistResult playlist;
        try
        {
            playlist = await CreatePlaylistInternal(playlistName, matchedIds, user, request.Public);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Jellyfin playlist");
            return StatusCode(500, new { operationId, error = $"Failed to create Jellyfin playlist: {ex.Message}" });
        }

        _logger.LogInformation("Created playlist '{Name}' ({Id}) with {Count} songs",
            playlistName, playlist.PlaylistId, matchedIds.Count);

        return new ImportResult
        {
            OperationId = operationId,
            PlaylistId = playlist.PlaylistId,
            PlaylistName = playlistName,
            MatchedCount = matchedIds.Count,
            UnmatchedCount = unmatched.Count,
            TotalCount = neteaseData.Songs.Count,
            UnmatchedSongs = unmatched
        };
    }

    /// <summary>
    /// Create a new playlist in Jellyfin.
    /// </summary>
    [HttpPost("CreatePlaylist")]
    public async Task<ActionResult<PlaylistResult>> CreatePlaylist(
        [FromBody] CreatePlaylistRequest request, CancellationToken ct)
    {
        var operationId = NewOperationId();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["Endpoint"] = "CreatePlaylist"
        });

        _logger.LogInformation("Create playlist request started with {SongCount} songs", request.SongItemIds.Count);

        var user = await GetCurrentUser();
        if (user == null)
        {
            _logger.LogError("Create playlist failed: no Jellyfin request user found");
            return StatusCode(401, new { operationId, error = "No Jellyfin request user found" });
        }

        try
        {
            var playlist = await CreatePlaylistInternal(request.Name, request.SongItemIds, user, request.Public);
            playlist.OperationId = operationId;

            _logger.LogInformation("Created playlist '{Name}' ({PlaylistId}) with {SongCount} songs",
                playlist.Name, playlist.PlaylistId, request.SongItemIds.Count);

            return playlist;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Jellyfin playlist");
            return StatusCode(500, new { operationId, error = $"Failed to create Jellyfin playlist: {ex.Message}" });
        }
    }

    /// <summary>
    /// Add songs to an existing Jellyfin playlist.
    /// </summary>
    [HttpPost("AddSongs")]
    public async Task<ActionResult<PlaylistResult>> AddSongs(
        [FromBody] AddSongsRequest request, CancellationToken ct)
    {
        var operationId = NewOperationId();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["Endpoint"] = "AddSongs",
            ["PlaylistId"] = request.PlaylistId
        });

        _logger.LogInformation("Add songs request started with {SongCount} songs", request.SongItemIds.Count);

        if (!Guid.TryParse(request.PlaylistId, out var playlistGuid))
        {
            _logger.LogWarning("Add songs request rejected: invalid playlist ID");
            return BadRequest(new { error = "Invalid playlist ID" });
        }

        var playlist = _libraryManager.GetItemById(playlistGuid) as Playlist;
        if (playlist == null)
        {
            _logger.LogWarning("Add songs request failed: playlist not found");
            return NotFound(new { error = "Playlist not found" });
        }

        var existingIds = new HashSet<string>(
            (playlist.LinkedChildren ?? Array.Empty<LinkedChild>())
            .Select(lc => lc.ItemId.ToString())
            .Where(id => id != null)
            .Select(id => id!));

        var newChildren = new List<LinkedChild>(playlist.LinkedChildren ?? Array.Empty<LinkedChild>());
        var addedCount = 0;
        foreach (var songId in request.SongItemIds)
        {
            if (!existingIds.Contains(songId) && Guid.TryParse(songId, out var songGuid))
            {
                newChildren.Add(new LinkedChild { ItemId = songGuid });
                addedCount++;
            }
        }

        playlist.LinkedChildren = newChildren.ToArray();
        await _libraryManager.UpdateItemAsync(playlist, playlist, ItemUpdateType.MetadataEdit, ct);
        _logger.LogInformation("Added {AddedCount} songs to playlist '{Name}' ({PlaylistId})",
            addedCount, playlist.Name, playlist.Id);

        return new PlaylistResult
        {
            OperationId = operationId,
            PlaylistId = playlist.Id.ToString(),
            Name = playlist.Name ?? "",
            SongCount = newChildren.Count,
            DateCreated = playlist.DateCreated
        };
    }

    /// <summary>
    /// List all playlists in the Jellyfin library.
    /// </summary>
    [HttpGet("Playlists")]
    public ActionResult<PlaylistListResult> ListPlaylists()
    {
        var operationId = NewOperationId();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["Endpoint"] = "Playlists"
        });

        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Playlist }
        };
        var playlists = _libraryManager.GetItemList(query);
        _logger.LogInformation("Listed {Count} playlists", playlists.Count);

        return new PlaylistListResult
        {
            OperationId = operationId,
            Playlists = playlists.Select(p => new PlaylistResult
            {
                OperationId = operationId,
                PlaylistId = p.Id.ToString(),
                Name = p.Name ?? "",
                SongCount = (p as Playlist)?.LinkedChildren?.Length ?? 0,
                DateCreated = p.DateCreated
            }).ToList()
        };
    }

    /// <summary>
    /// Get a specific playlist with details.
    /// </summary>
    [HttpGet("Playlist/{playlistId}")]
    public ActionResult<PlaylistResult> GetPlaylist(string playlistId)
    {
        var operationId = NewOperationId();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["Endpoint"] = "Playlist",
            ["PlaylistId"] = playlistId
        });

        if (!Guid.TryParse(playlistId, out var guid))
        {
            _logger.LogWarning("Get playlist request rejected: invalid playlist ID");
            return BadRequest(new { error = "Invalid playlist ID" });
        }

        var playlist = _libraryManager.GetItemById(guid) as Playlist;
        if (playlist == null)
        {
            _logger.LogWarning("Get playlist failed: playlist not found");
            return NotFound(new { error = "Playlist not found" });
        }

        _logger.LogInformation("Found playlist '{Name}' ({PlaylistId}) with {SongCount} songs",
            playlist.Name, playlist.Id, playlist.LinkedChildren?.Length ?? 0);

        return new PlaylistResult
        {
            OperationId = operationId,
            PlaylistId = playlist.Id.ToString(),
            Name = playlist.Name ?? "",
            SongCount = playlist.LinkedChildren?.Length ?? 0,
            DateCreated = playlist.DateCreated
        };
    }

    /// <summary>
    /// Delete a playlist.
    /// </summary>
    [HttpDelete("Playlist/{playlistId}")]
    public ActionResult<DeleteResult> DeletePlaylist(string playlistId)
    {
        var operationId = NewOperationId();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["Endpoint"] = "DeletePlaylist",
            ["PlaylistId"] = playlistId
        });

        if (!Guid.TryParse(playlistId, out var guid))
        {
            _logger.LogWarning("Delete playlist request rejected: invalid playlist ID");
            return BadRequest(new { error = "Invalid playlist ID" });
        }

        var playlist = _libraryManager.GetItemById(guid);
        if (playlist == null)
        {
            _logger.LogWarning("Delete playlist failed: playlist not found");
            return NotFound(new { error = "Playlist not found" });
        }

        var playlistName = playlist.Name;
        _libraryManager.DeleteItem(playlist, new DeleteOptions());
        _logger.LogInformation("Deleted playlist '{Name}' ({PlaylistId})", playlistName, playlistId);

        return new DeleteResult { OperationId = operationId, Success = true, Message = $"Playlist '{playlistName}' deleted" };
    }

    /// <summary>
    /// Search the Jellyfin library for songs.
    /// </summary>
    [HttpGet("SearchSongs")]
    public async Task<ActionResult<List<SongSearchResult>>> SearchSongs(
        [FromQuery] string query, CancellationToken ct, [FromQuery] int maxResults = 20)
    {
        var operationId = NewOperationId();
        using var scope = _logger.BeginScope(new Dictionary<string, object>
        {
            ["OperationId"] = operationId,
            ["Endpoint"] = "SearchSongs"
        });

        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("Search songs request rejected: empty query");
            return BadRequest(new { error = "Query parameter is required" });
        }

        var results = await _matcher.SearchSongsAsync(query, maxResults, ct);
        foreach (var result in results)
            result.OperationId = operationId;

        _logger.LogInformation("Search songs returned {Count} results", results.Count);
        return results;
    }

    private async Task<PlaylistResult> CreatePlaylistInternal(string name, List<string> songItemIds, User user, bool isPublic)
    {
        var itemIds = songItemIds
            .Where(id => Guid.TryParse(id, out _))
            .Select(Guid.Parse)
            .ToList();

        var result = await _playlistManager.CreatePlaylist(new PlaylistCreationRequest
        {
            Name = name,
            UserId = user.Id,
            ItemIdList = itemIds,
            MediaType = MediaType.Audio,
            Public = isPublic
        });

        _logger.LogInformation("Playlist created for user {UserId} with public={Public} and playlistId={PlaylistId}",
            user.Id, isPublic, result.Id);

        var playlistId = Guid.Parse(result.Id);
        var playlist = _libraryManager.GetItemById(playlistId) as Playlist;

        return new PlaylistResult
        {
            PlaylistId = result.Id,
            Name = playlist?.Name ?? name,
            SongCount = itemIds.Count,
            DateCreated = playlist?.DateCreated ?? DateTime.UtcNow
        };
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
}
