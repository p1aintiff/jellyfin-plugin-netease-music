using System.Text;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.NetEaseMusic.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NetEaseMusic.Services;

public class SongMatcher
{
    private readonly ILibraryManager _libraryManager;
    private readonly ILogger<SongMatcher> _logger;

    public SongMatcher(ILibraryManager libraryManager, ILogger<SongMatcher> logger)
    {
        _libraryManager = libraryManager;
        _logger = logger;
    }

    public Task<string?> FindMatchAsync(NetEaseSongData song, CancellationToken ct = default)
    {
        // Search for candidates, then require the same song name and a shared artist.
        var candidates = SearchByName(song.Name, 30);
        _logger.LogDebug("Found {CandidateCount} candidates for '{SongName}'", candidates.Count, song.Name);
        if (candidates.Count == 0)
        {
            _logger.LogDebug("No candidates found for '{SongName}'", song.Name);
            return Task.FromResult<string?>(null);
        }

        var name = Normalize(song.Name);
        var artists = song.Artists.Select(Normalize).Where(artist => artist.Length > 0).ToHashSet();
        foreach (var item in candidates)
        {
            if (item is not Audio audio || Normalize(audio.Name) != name) continue;
            if (audio.Artists.Any(artist => artists.Contains(Normalize(artist))))
            {
                _logger.LogDebug("Matched '{Song}' -> Jellyfin item {ItemId}", song.Name, audio.Id);
                return Task.FromResult<string?>(audio.Id.ToString());
            }
        }

        _logger.LogDebug("No match for '{Song}' by {Artists}",
            song.Name, string.Join(", ", song.Artists));
        return Task.FromResult<string?>(null);
    }

    private List<BaseItem> SearchByName(string name, int limit)
    {
        var query = new InternalItemsQuery
        {
            IncludeItemTypes = new[] { BaseItemKind.Audio },
            SearchTerm = name,
            Limit = limit,
            Recursive = true
        };
        return _libraryManager.GetItemList(query).ToList();
    }

    private static string Normalize(string s)
    {
        return new string(s.Normalize(NormalizationForm.FormKC)
            .Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();
    }
}
