using Jellyfin.Plugin.NetEaseMusic.Services;
using Microsoft.Extensions.Logging;

var url = args.Length > 0
    ? args[0]
    : "https://music.163.com/m/playlist?id=13822175569&creatorId=7899239998";

using var httpClient = new HttpClient();
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
    "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) " +
    "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1");
httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/json");
httpClient.DefaultRequestHeaders.Add("Referer", "https://music.163.com/");

var scraper = new NetEaseScraper(httpClient, new ConsoleLogger<NetEaseScraper>());
var playlist = await scraper.ScrapePlaylistAsync(url);

Console.WriteLine($"PlaylistId: {playlist.PlaylistId}");
Console.WriteLine($"Name: {playlist.Name}");
Console.WriteLine($"TrackCount: {playlist.TrackCount}");
Console.WriteLine($"Songs: {playlist.Songs.Count}");

foreach (var song in playlist.Songs.Take(5))
{
    Console.WriteLine($"{song.Name} - {string.Join(", ", song.Artists)} - {song.Album} - {song.DurationMs}");
}

sealed class ConsoleLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (IsEnabled(logLevel))
            Console.Error.WriteLine(formatter(state, exception));
    }
}
