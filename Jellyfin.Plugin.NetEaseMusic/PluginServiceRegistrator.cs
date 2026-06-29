using Jellyfin.Plugin.NetEaseMusic.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.NetEaseMusic;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
    {
        services.AddSingleton<SongMatcher>();

        services.AddHttpClient<NetEaseScraper>(client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) " +
                "AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1");
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json,text/html,application/xhtml+xml");
            client.DefaultRequestHeaders.Add("Referer", "https://music.163.com/");
        });
    }
}
