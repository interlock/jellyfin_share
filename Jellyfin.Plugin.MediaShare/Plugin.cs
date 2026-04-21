using System.IO;
using Jellyfin.Plugin.MediaShare.Configuration;
using Jellyfin.Plugin.MediaShare.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaShare;

public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginId = Guid.Parse("613657e0-25b9-4d58-9f4d-1496726b0532");

    public override string Name => "Media Share";

    public Plugin(
        IServerApplicationHost serverHost,
        IApplicationPaths appPaths,
        IXmlSerializer xml,
        ILibraryManager libraryManager,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
        : base(appPaths, xml)
    {
        var dataDir = Path.Combine(appPaths.PluginConfigurationsPath, Name);
        Directory.CreateDirectory(dataDir);

        var scheme = serverHost.ListenWithHttps ? "https" : "http";
        var port = serverHost.ListenWithHttps ? serverHost.HttpsPort : serverHost.HttpPort;
        var serverUrl = $"{scheme}://{serverHost.FriendlyName}:{port}";

        LinkService = new ShareLinkService(
            () => Configuration,
            cfg => Configuration = cfg,
            serverUrl);

        FedService = new FederationService(
            () => Configuration,
            cfg => Configuration = cfg,
            libraryManager,
            httpClientFactory,
            loggerFactory.CreateLogger<FederationService>());

    }

    public ShareLinkService LinkService { get; }
    public FederationService FedService { get; }

    public void SetConfiguration(PluginConfiguration cfg) => Configuration = cfg;

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                // EnableInMainMenu = true,
                // MenuSection = "server",
                // MenuIcon = "share",
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            }
        ];
    }
}