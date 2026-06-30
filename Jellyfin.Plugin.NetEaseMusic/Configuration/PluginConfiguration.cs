using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.NetEaseMusic;

public class PluginConfiguration : BasePluginConfiguration
{
    public double MatchThreshold { get; set; } = 0.6;
    public bool ReportUnmatched { get; set; } = true;
    public int ScrapeTimeoutSeconds { get; set; } = 15;
    public List<CachedImport> CachedImports { get; set; } = new();
}

public class CachedImport
{
    public string NetEaseUrl { get; set; } = string.Empty;
    public string JellyfinPlaylistId { get; set; } = string.Empty;
}
