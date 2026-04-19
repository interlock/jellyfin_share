using Jellyfin.Plugin.MediaShare.Data;
using Jellyfin.Plugin.MediaShare.Models;

namespace Jellyfin.Plugin.MediaShare.Services;

public class ShareLinkService(ShareDbContext db, string serverUrl)
{
    public string CreateShareLink(string libraryId, string libraryName, TimeSpan? expiresIn)
    {
        var link = new ShareLink
        {
            LibraryId = libraryId,
            InviteCode = Guid.NewGuid().ToString("N"),
            ExpiresAt = expiresIn.HasValue ? DateTime.UtcNow.Add(expiresIn.Value) : null
        };
        db.Links.Insert(link);
        return $"{serverUrl}/api/mediashare/invite/{link.InviteCode}";
    }

    public SharedLibrary? ValidateInviteCode(string code, long defaultExpirySeconds = 604800)
    {
        var link = db.Links.FindOne(l => l.InviteCode == code && !l.IsRevoked);
        if (link is null) return null;

        var effectiveExpiry = link.ExpiresAt;
        if (link.UsesDefaultExpiry && effectiveExpiry is null)
        {
            // Recompute from creation time + current default
            effectiveExpiry = defaultExpirySeconds < 0
                ? (DateTime?)null
                : link.CreatedAt.AddSeconds(defaultExpirySeconds);
        }

        if (effectiveExpiry.HasValue && effectiveExpiry < DateTime.UtcNow) return null;
        return new SharedLibrary
        {
            LibraryId = link.LibraryId,
            PeerShareLinkId = link.Id,
            PeerServerUrl = serverUrl,
            IsIncoming = false
        };
    }

    public ShareLink? GetLinkById(string id)
        => db.Links.FindOne(l => l.Id == id && !l.IsRevoked);

    public IEnumerable<ShareLink> GetLinksForLibrary(string libraryId)
        => db.Links.Find(l => l.LibraryId == libraryId);

    public void RevokeLink(string id)
    {
        var link = db.Links.FindOne(l => l.Id == id);
        if (link is not null)
        {
            link.IsRevoked = true;
            db.Links.Update(link);
        }
    }

    public IEnumerable<ShareLink> GetAllActiveLinks()
        => db.Links.Find(l => !l.IsRevoked);
}