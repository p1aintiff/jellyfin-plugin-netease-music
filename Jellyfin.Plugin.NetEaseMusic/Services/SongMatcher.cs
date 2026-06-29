using Jellyfin.Data.Entities;
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
        // Try exact match first: song name
        var candidates = SearchByName(song.Name, 30);
        _logger.LogDebug("Found {CandidateCount} candidates for '{SongName}'", candidates.Count, song.Name);
        if (candidates.Count == 0)
        {
            _logger.LogDebug("No candidates found for '{SongName}'", song.Name);
            return Task.FromResult<string?>(null);
        }

        // Try to match by artist
        foreach (var item in candidates)
        {
            if (item is not Audio audio) continue;
            if (ArtistMatches(audio, song.Artists))
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

    private static bool ArtistMatches(Audio audio, List<string> neteaseArtists)
    {
        var jellyfinArtists = audio.Artists ?? Array.Empty<string>();
        foreach (var na in neteaseArtists)
        {
            var normalized = Normalize(na);
            foreach (var ja in jellyfinArtists)
            {
                if (Normalize(ja).Contains(normalized) || normalized.Contains(Normalize(ja)))
                    return true;
            }
        }
        return false;
    }

    private static string Normalize(string s)
    {
        return s.ToLowerInvariant()
            .Replace("(", "").Replace(")", "")
            .Replace("[", "").Replace("]", "")
            .Replace("（", "").Replace("）", "")
            .Replace("【", "").Replace("】", "")
            .Replace("'", "").Replace("\"", "")
            .Replace("&", "").Replace("、", " ")
            .Replace("feat.", " ").Replace("Feat.", " ")
            .Replace("FEAT.", " ").Replace("ft.", " ")
            .Replace("with", " ").Replace("With", " ")
            .Replace("cover", "").Replace("Cover", "")
            .Replace("remix", "").Replace("Remix", "")
            .Replace("live", "").Replace("Live", "")
            .Replace("(", "").Replace(")", "")
            .Trim();
    }
}
