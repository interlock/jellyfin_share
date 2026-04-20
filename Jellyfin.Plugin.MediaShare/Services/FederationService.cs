using System.Net.Http.Json;
using Jellyfin.Plugin.MediaShare.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaShare.Services;

public class FederationService(
    Func<PluginConfiguration> getConfig,
    Action<PluginConfiguration> saveConfig,
    ILibraryManager libraryManager,
    IHttpClientFactory httpClientFactory,
    ILogger<FederationService> logger)
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("Jellyfin.Plugin.MediaShare");

    public List<Models.MediaItem> BuildCatalogFromLibrary(string libraryId)
    {
        if (!Guid.TryParse(libraryId, out var guid))
        {
            logger.LogWarning("Invalid library ID format (expected GUID): {LibraryId}", libraryId);
            return [];
        }

        var folder = libraryManager.GetItemById(guid) as Folder;
        if (folder is null || string.IsNullOrEmpty(folder.Path))
        {
            logger.LogWarning("Library not found or has no path: {LibraryId}", libraryId);
            return [];
        }

        var catalog = new List<Models.MediaItem>();
        var query = new InternalItemsQuery { AncestorIds = [folder.Id] };
        foreach (var item in libraryManager.GetItemList(query))
        {
            if (item is Video video)
                catalog.Add(ToMediaItem(video));
        }
        return catalog;
    }

    private static Models.MediaItem ToMediaItem(Video video)
    {
        var sources = video.GetMediaSources(false);
        return new Models.MediaItem
        {
            Id = video.Id.ToString(),
            Title = video.Name,
            Year = video.ProductionYear,
            MediaType = video.MediaType.ToString(),
            PosterUrl = video.PrimaryImagePath,
            Overview = video.Overview,
            Files = sources.Select(ms => new Models.MediaFile
            {
                Id = ms.Path,
                Path = ms.Path,
                Size = ms.RunTimeTicks ?? 0
            }).ToList()
        };
    }

    public async Task TriggerSyncAsync()
    {
        var config = getConfig();
        foreach (var incoming in config.IncomingShares)
        {
            if (string.IsNullOrEmpty(incoming.PeerServerUrl)) continue;
            await SyncIncomingShareAsync(incoming, config);
        }
    }

    private async Task SyncIncomingShareAsync(IncomingShare incoming, PluginConfiguration config)
    {
        try
        {
            var code = incoming.PeerShareLinkId ?? incoming.Id;
            var resp = await _http.GetAsync($"{incoming.PeerServerUrl}/mediashare/share/{code}/catalog");
            if (!resp.IsSuccessStatusCode) return;

            var items = await resp.Content.ReadFromJsonAsync<List<Models.MediaItem>>();
            if (items is null || items.Count == 0) return;

            GenerateStrmFiles(incoming, items);
            incoming.ItemCount = items.Count;
            incoming.SyncedAt = DateTime.UtcNow;
            saveConfig(config);
            _ = libraryManager.ValidateMediaLibrary(new Progress<double>(), CancellationToken.None);
            logger.LogInformation("Synced {Count} items from {Url}", items.Count, incoming.PeerServerUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync from {Url}", incoming.PeerServerUrl);
        }
    }

    public async Task AcceptInviteAsync(string inviteUrl)
    {
        var parts = ParseInviteUrl(inviteUrl);
        if (!parts.HasValue) return;

        var (peerServerUrl, code) = parts.Value;
        var config = getConfig();
        if (config.IncomingShares.Any(s => s.Id == code)) return;

        var resp = await _http.GetAsync($"{peerServerUrl}/mediashare/share/{code}/catalog");
        if (!resp.IsSuccessStatusCode) return;

        var items = await resp.Content.ReadFromJsonAsync<List<Models.MediaItem>>();
        var name = items?.FirstOrDefault()?.Title.Split('/')[0] ?? "Shared Library";

        var incoming = new IncomingShare
        {
            Id = code,
            PeerServerUrl = peerServerUrl,
            PeerShareLinkId = code,
            LibraryName = $"Shared: {name}",
            AddedAt = DateTime.UtcNow,
            ItemCount = items?.Count ?? 0
        };
        config.IncomingShares.Add(incoming);

        if (items is not null)
            GenerateStrmFiles(incoming, items);

        saveConfig(config);
        _ = libraryManager.ValidateMediaLibrary(new Progress<double>(), CancellationToken.None);
    }

    private void GenerateStrmFiles(IncomingShare incoming, List<Models.MediaItem> items)
    {
        var root = GetShareRoot(incoming.Id);
        Directory.CreateDirectory(root);

        foreach (var item in items)
        {
            foreach (var file in item.Files)
            {
                var encodedId = $"{incoming.PeerServerUrl}::{file.Path}";
                var strm = Path.Combine(root, $"{SanitizeFileName(item.Title)}.strm");
                File.WriteAllText(strm, $"/mediashare/stream/{incoming.Id}/{Uri.EscapeDataString(encodedId)}");

                var nfo = Path.Combine(root, $"{SanitizeFileName(item.Title)}.nfo");
                File.WriteAllText(nfo, $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <movie>
                      <title>{System.Security.SecurityElement.Escape(item.Title)}</title>
                      <year>{item.Year ?? 0}</year>
                    </movie>
                    """);

                // Track .strm path for local streaming
                incoming.FileMap[file.Path] = strm;
            }
        }
    }

    private static string GetShareRoot(string shareId) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Jellyfin.Plugin.MediaShare", "shared", shareId);

    private static (string serverUrl, string code)? ParseInviteUrl(string url)
    {
        var uri = new Uri(url);
        var segments = uri.Segments;
        if (segments.Length < 4) return null;
        var code = segments[^1].TrimEnd('/');
        return ($"{uri.Scheme}://{uri.Host}:{uri.Port}", code);
    }

    private static string SanitizeFileName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}