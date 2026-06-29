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

        _logger.LogInformation("Import request URL length is {Length}", request.Url.Length);

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
            ItemIdList = itemIds,
            MediaType = MediaType.Audio,
            Public = isPublic
        });

        _logger.LogInformation("Playlist created for user {UserId} with public={Public} and playlistId={PlaylistId}",
            user.Id, isPublic, result.Id);

        var playlistId = Guid.Parse(result.Id);
        var playlist = _libraryManager.GetItemById(playlistId) as Playlist;

        return new ImportPlaylistResult
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
