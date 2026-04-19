using System.IO;
using JellyfinMediaShare.Configuration;
using JellyfinMediaShare.Data;
using JellyfinMediaShare.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace JellyfinMediaShare;

public class SharePlugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public static readonly Guid PluginId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");

    public override string Name => "Media Share";

    private readonly ShareDbContext _db;
    private readonly string _dbPath;

    public SharePlugin(
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
        var serverUrl = "http://localhost:8096";

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