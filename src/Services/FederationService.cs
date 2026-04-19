using System.Net.Http.Json;
using System.Text.Json;
using JellyfinMediaShare.Data;
using JellyfinMediaShare.Models;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace JellyfinMediaShare.Services;

public class FederationService(
    ShareDbContext db,
    ILibraryManager libraryManager,
    IHttpClientFactory httpClientFactory,
    ILogger<FederationService> logger)
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("JellyfinMediaShare");

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

            await GenerateStrmFilesAsync(shareLinkId, items);
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

    private async Task GenerateStrmFilesAsync(string shareLinkId, List<MediaItem> items)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "JellyfinMediaShare", "shared", shareLinkId);
        Directory.CreateDirectory(root);

        foreach (var item in items)
        {
            foreach (var file in item.Files)
            {
                var strmPath = Path.Combine(root, SanitizeFileName(item.Title) + ".strm");
                var strmContent = $"/mediashare/stream/{shareLinkId}/{file.Id}";
                await File.WriteAllTextAsync(strmPath, strmContent);

                var nfoPath = Path.Combine(root, Path.GetFileNameWithoutExtension(strmPath) + ".nfo");
                var nfoContent = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<movie>
  <title>{System.Security.SecurityElement.Escape(item.Title)}</title>
  <year>{item.Year}</year>
</movie>";
                await File.WriteAllTextAsync(nfoPath, nfoContent);
            }
        }

        // libraryManager.AddMediaPath(root, new MediaPathInfo()); // not available in jellyfin 10.11
    }

    private static (string serverUrl, string code)? ParseInviteUrl(string url)
    {
        var uri = new Uri(url);
        var segments = uri.Segments;
        // expects /api/mediashare/invite/{code}
        if (segments.Length < 4) throw new InvalidOperationException("Invalid invite URL");
        var code = segments[^1].TrimEnd('/');
        var baseUrl = $"{uri.Scheme}://{uri.Host}:{uri.Port}";
        return (baseUrl, code);
    }

    private static string SanitizeFileName(string name)
        => string.Join("_", name.Split(Path.GetInvalidFileNameChars()));
}