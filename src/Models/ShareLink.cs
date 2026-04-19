namespace JellyfinMediaShare.Models;

public class ShareLink
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string LibraryId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    /// True = ExpiresAt was computed from the default expiry at creation time (dynamic).
    public bool UsesDefaultExpiry { get; set; } = true;
    public bool IsRevoked { get; set; }
    public string? InviteCode { get; set; }
}