using System.Text.Json;
using System.Text.RegularExpressions;
using Jellyfin.Plugin.NetEaseMusic.Models;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.NetEaseMusic.Services;

public class NetEaseScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NetEaseScraper> _logger;

    public NetEaseScraper(HttpClient httpClient, ILogger<NetEaseScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<NetEasePlaylistData> ScrapePlaylistAsync(string playlistUrl, CancellationToken ct = default)
    {
        var match = Regex.Match(playlistUrl, @"(?:[?&]id=|playlist/)(\d+)");
        if (!match.Success)
            throw new ArgumentException($"Invalid NetEase playlist URL: {playlistUrl}");

        var playlistId = match.Groups[1].Value;
        _logger.LogInformation("Scraping NetEase playlist {PlaylistId}", playlistId);

        var apiUrl = $"https://music.163.com/api/v6/playlist/detail?id={playlistId}&n=1000";

        using var response = await _httpClient.GetAsync(apiUrl, ct);
        _logger.LogInformation("NetEase playlist API returned {StatusCode}", (int)response.StatusCode);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = json.RootElement;

        if (TryGetInt64(root, "code") != 200)
        {
            _logger.LogWarning("NetEase playlist API returned invalid code {Code}", TryGetInt64(root, "code"));
            throw new InvalidOperationException("NetEase API returned an invalid response.");
        }

        var playlist = TryGetProp(root, "result") ?? TryGetProp(root, "playlist")
            ?? throw new InvalidOperationException("NetEase API response has no playlist.");

        var tracks = TryGetProp(playlist, "tracks");
        var songs = tracks?.ValueKind == JsonValueKind.Array
            ? ExtractSongs(tracks.Value)
            : new List<NetEaseSongData>();
        _logger.LogInformation("NetEase playlist API returned {TrackCount} tracks", songs.Count);

        if (TryGetProp(playlist, "trackIds") is JsonElement trackIdsElement &&
            trackIdsElement.ValueKind == JsonValueKind.Array)
        {
            var trackIds = new List<long>();
            foreach (var item in trackIdsElement.EnumerateArray())
            {
                var id = TryGetInt64(item, "id");
                if (id > 0)
                    trackIds.Add(id);
            }

            _logger.LogInformation("NetEase playlist API returned {TrackIdCount} track IDs", trackIds.Count);

            if (trackIds.Count > songs.Count)
            {
                var detailSongs = await FetchSongDetailsAsync(trackIds, ct);
                _logger.LogInformation("NetEase song detail API returned {DetailCount} songs", detailSongs.Count);
                if (detailSongs.Count > songs.Count)
                    songs = detailSongs;
            }
        }
        else
        {
            _logger.LogWarning("NetEase playlist API response has no trackIds array");
        }

        if (songs.Count == 0)
        {
            _logger.LogWarning("NetEase playlist has no parsed songs");
            throw new InvalidOperationException("NetEase playlist has no songs.");
        }

        return new NetEasePlaylistData
        {
            Name = TryGetString(playlist, "name") ?? "Imported Playlist",
            Description = TryGetString(playlist, "description") ?? "",
            PlaylistId = playlistId,
            TrackCount = TryGetInt64(playlist, "trackCount"),
            Songs = songs
        };
    }

    private List<NetEaseSongData> ExtractSongs(JsonElement tracks)
    {
        var songs = new List<NetEaseSongData>();
        foreach (var track in tracks.EnumerateArray())
        {
            var song = ParseSongData(track);
            if (song != null) songs.Add(song);
        }
        return songs;
    }

    private async Task<List<NetEaseSongData>> FetchSongDetailsAsync(List<long> trackIds, CancellationToken ct)
    {
        var songs = new List<NetEaseSongData>();

        foreach (var batch in trackIds.Chunk(100))
        {
            var ids = Uri.EscapeDataString($"[{string.Join(",", batch)}]");
            using var response = await _httpClient.GetAsync($"https://music.163.com/api/song/detail?ids={ids}", ct);
            _logger.LogDebug("NetEase song detail API batch returned {StatusCode}", (int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Skipped NetEase song detail batch because status was {StatusCode}", (int)response.StatusCode);
                continue;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            var details = TryGetProp(json.RootElement, "songs");
            if (details?.ValueKind == JsonValueKind.Array)
            {
                songs.AddRange(ExtractSongs(details.Value));
            }
            else
            {
                _logger.LogWarning("NetEase song detail response has no songs array");
            }
        }

        return songs;
    }

    private NetEaseSongData? ParseSongData(JsonElement track)
    {
        var name = TryGetString(track, "name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var artists = new List<string>();
        if (TryGetProp(track, "ar") is JsonElement ar && ar.ValueKind == JsonValueKind.Array)
        {
            foreach (var artist in ar.EnumerateArray())
            {
                var artistName = TryGetString(artist, "name");
                if (!string.IsNullOrWhiteSpace(artistName))
                    artists.Add(artistName);
            }
        }
        else if (TryGetProp(track, "artists") is JsonElement artistsElement && artistsElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var artist in artistsElement.EnumerateArray())
            {
                var artistName = TryGetString(artist, "name");
                if (!string.IsNullOrWhiteSpace(artistName))
                    artists.Add(artistName);
            }
        }

        var album = TryGetString(track, "al", "name")
            ?? TryGetString(track, "album", "name")
            ?? "";
        var duration = TryGetInt64(track, "dt");
        if (duration == 0)
            duration = TryGetInt64(track, "duration");

        return new NetEaseSongData
        {
            Name = name,
            Artists = artists,
            Album = album,
            DurationMs = duration
        };
    }

    // --- JSON helpers ---

    private static string? TryGetString(JsonElement element, params object[] path)
    {
        foreach (var segment in path)
        {
            if (segment is string key)
            {
                if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(key, out element))
                    return null;
            }
            else if (segment is int index)
            {
                if (element.ValueKind != JsonValueKind.Array || index < 0 || index >= element.GetArrayLength())
                    return null;

                element = element[index];
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static JsonElement? TryGetProp(JsonElement element, params string[] path)
    {
        foreach (var key in path)
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(key, out element))
                return null;
        }
        return element;
    }

    private static long TryGetInt64(JsonElement element, params string[] path)
    {
        var prop = TryGetProp(element, path);
        if (prop == null) return 0;
        return prop.Value.ValueKind switch
        {
            JsonValueKind.Number => prop.Value.TryGetInt64(out var v) ? v : 0,
            _ => 0
        };
    }
}
