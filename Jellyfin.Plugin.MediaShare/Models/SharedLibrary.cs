namespace Jellyfin.Plugin.MediaShare.Models;

public class SharedLibrary
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string LibraryId { get; set; } = string.Empty;
    public string LibraryName { get; set; } = string.Empty;
    public string? PeerServerUrl { get; set; }
    public string? PeerShareLinkId { get; set; }
    public DateTime SyncedAt { get; set; } = DateTime.UtcNow;
    public bool IsIncoming { get; set; }
}