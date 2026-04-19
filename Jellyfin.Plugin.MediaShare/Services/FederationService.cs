using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.MediaShare.Data;
using Jellyfin.Plugin.MediaShare.Models;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaShare.Services;

public class FederationService(
    ShareDbContext db,
    ILibraryManager libraryManager,
    IHttpClientFactory httpClientFactory,
    ILogger<FederationService> logger)
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("Jellyfin.Plugin.MediaShare");

    public async Task SyncIncomingShareAsync(string peerServerUrl, string shareLinkId)
    {
        try
        {
            var resp = await _http.GetAsync($"{peerServerUrl}/mediashare/share/{shareLinkId}/catalog");
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Failed to fetch catalog from {Url}: {Status}", peerServerUrl, resp.StatusCode);
                return;
            }

            var items = await resp.Content.ReadFromJsonAsync<List<MediaItem>>();
            if (items is null) return;

            var existing = db.Libraries.FindOne(l => l.PeerShareLinkId == shareLinkId);
            if (existing is not null)
            {
                existing.SyncedAt = DateTime.UtcNow;
                db.Libraries.Update(existing);
            }

            GenerateStrmFiles(shareLinkId, items);
            libraryManager.CreateShortcut(GetShareRoot(shareLinkId), new MediaPathInfo());
            libraryManager.QueueLibraryScan();
            logger.LogInformation("Synced {Count} items from peer {Url}", items.Count, peerServerUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to sync catalog from {Url}", peerServerUrl);
        }
    }

    public async Task AcceptInviteAsync(string inviteUrl)
    {
        var parts = ParseInviteUrl(inviteUrl);
        if (!parts.HasValue) throw new InvalidOperationException("Invalid invite URL format");

        var (peerServerUrl, code) = parts.Value;

        var library = await FetchLibraryMetadataAsync(peerServerUrl, code);
        if (library is null) throw new InvalidOperationException("Could not fetch library metadata from peer");

        library.IsIncoming = true;
        library.PeerServerUrl = peerServerUrl;
        db.Libraries.Insert(library);

        await SyncIncomingShareAsync(peerServerUrl, code);
    }

    public async Task TriggerSyncAsync()
    {
        var incoming = db.Libraries.Find(l => l.IsIncoming && !string.IsNullOrEmpty(l.PeerShareLinkId));
        foreach (var lib in incoming)
        {
            if (!string.IsNullOrEmpty(lib.PeerServerUrl))
                await SyncIncomingShareAsync(lib.PeerServerUrl, lib.PeerShareLinkId!);
        }
    }

    private async Task<SharedLibrary?> FetchLibraryMetadataAsync(string peerServerUrl, string code)
    {
        var resp = await _http.GetAsync($"{peerServerUrl}/mediashare/share/{code}/catalog");
        if (!resp.IsSuccessStatusCode) return null;

        var items = await resp.Content.ReadFromJsonAsync<List<MediaItem>>();
        if (items is null || items.Count == 0) return null;

        var name = items[0].Title.Split('/')[0];
        return new SharedLibrary
        {
            LibraryId = code,
            LibraryName = $"Shared: {name}",
            PeerShareLinkId = code,
            PeerServerUrl = peerServerUrl,
            IsIncoming = true
        };
    }

    public List<MediaItem> BuildCatalogFromLibrary(string libraryId)
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

        return CollectMediaItems(folder, new());
    }

    private List<MediaItem> CollectMediaItems(Folder parent, List<MediaItem> catalog)
    {
        var query = new InternalItemsQuery { AncestorIds = [parent.Id] };
        var items = libraryManager.GetItemList(query);

        foreach (var item in items)
        {
            if (item is Video video)
            {
                catalog.Add(ToMediaItem(video));
            }
        }

        return catalog;
    }

    private MediaItem ToMediaItem(Video video)
    {
        var sources = video.GetMediaSources(false);
        return new MediaItem
        {
            Id = video.Id.ToString(),
            Title = video.Name,
            Year = video.ProductionYear,
            MediaType = video.MediaType.ToString(),
            PosterUrl = video.PrimaryImagePath,
            Overview = video.Overview,
            Files = sources.Select(ms => new MediaFile
            {
                Id = ms.Path,
                Path = ms.Path,
                Size = ms.RunTimeTicks ?? 0
            }).ToList()
        };
    }

    private void GenerateStrmFiles(string shareLinkId, List<MediaItem> items)
    {
        var root = GetShareRoot(shareLinkId);
        Directory.CreateDirectory(root);

        foreach (var item in items)
        {
            foreach (var file in item.Files)
            {
                var strm = Path.Combine(root, $"{SanitizeFileName(item.Title)}.strm");
                File.WriteAllText(strm, $"/mediashare/stream/{shareLinkId}/{Uri.EscapeDataString(file.Id)}");

                var nfo = Path.Combine(root, $"{SanitizeFileName(item.Title)}.nfo");
                var nfoContent = $"""
                    <?xml version="1.0" encoding="utf-8"?>
                    <movie>
                      <title>{System.Security.SecurityElement.Escape(item.Title)}</title>
                      <year>{item.Year ?? 0}</year>
                    </movie>
                    """;
                File.WriteAllText(nfo, nfoContent);
            }
        }
    }

    private static string GetShareRoot(string shareLinkId) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Jellyfin.Plugin.MediaShare", "shared", shareLinkId);

    private static (string serverUrl, string code)? ParseInviteUrl(string url)
    {
        var uri = new Uri(url);
        var segments = uri.Segments;
        if (segments.Length < 4) throw new InvalidOperationException("Invalid invite URL");
        var code = segments[^1].TrimEnd('/');
        var baseUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        return (baseUrl, code);
    }

    private static string SanitizeFileName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}