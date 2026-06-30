using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.NetEaseMusic;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginId = Guid.Parse("a1b2c3d4-0000-4000-8000-000000000001");

    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
    }

    public override string Name => "NetEase Music Importer";
    public override string Description => "Import NetEase Cloud Music playlists into Jellyfin.";
    public override Guid Id => PluginId;

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "neteasemusic-v016",
                DisplayName = "NetEase Music",
                EmbeddedResourcePath = GetType().Namespace + ".Web.configPage.html",
                EnableInMainMenu = true,
                MenuSection = "server",
                MenuIcon = "music_note"
            }
        };
    }
}
