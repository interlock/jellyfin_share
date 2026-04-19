using System.IO;
using Jellyfin.Plugin.MediaShare.Configuration;
using Jellyfin.Plugin.MediaShare.Data;
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

    private readonly ShareDbContext _db;
    private readonly string _dbPath;

    public Plugin(
        IServerApplicationHost serverHost,
        IApplicationPaths appPaths,
        IXmlSerializer xml,
        ILibraryManager libraryManager,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory)
        : base(appPaths, xml)
    {
        var dataDir = Path.Combine(appPaths.PluginConfigurationsPath, "MediaShare");
        Directory.CreateDirectory(dataDir);
        _dbPath = Path.Combine(dataDir, "mediashare.db");

        _db = new ShareDbContext(_dbPath);

        var scheme = serverHost.ListenWithHttps ? "https" : "http";
        var port = serverHost.ListenWithHttps ? serverHost.HttpsPort : serverHost.HttpPort;
        var serverUrl = $"{scheme}://{serverHost.FriendlyName}:{port}";

        LinkService = new ShareLinkService(_db, serverUrl);
        FedService = new FederationService(_db, libraryManager, httpClientFactory, loggerFactory.CreateLogger<FederationService>());
    }

    public ShareLinkService LinkService { get; }
    public FederationService FedService { get; }

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return
        [
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html"
            }
        ];
    }
}