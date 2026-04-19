using Jellyfin.Plugin.MediaShare.Data;
using Jellyfin.Plugin.MediaShare.Services;

namespace Jellyfin.Plugin.MediaShare.Tests;

public class ShareLinkServiceTests : IDisposable
{
    private readonly ShareDbContext _db;
    private readonly ShareLinkService _service;
    private readonly string _serverUrl = "http://localhost:8096";

    public ShareLinkServiceTests()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"jms_test_{Guid.NewGuid():N}.db");
        _db = new ShareDbContext(tempPath);
        _service = new ShareLinkService(_db, _serverUrl);
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void CreateShareLink_ReturnsNonNullUrl()
    {
        var url = _service.CreateShareLink("lib-1", "My Library", null);

        Assert.NotNull(url);
        Assert.NotEmpty(url);
    }

    [Fact]
    public void CreateShareLink_ReturnsUrlContainingInviteCode()
    {
        var url = _service.CreateShareLink("lib-1", "My Library", null);

        // URL format: {serverUrl}/api/mediashare/invite/{inviteCode}
        Assert.Contains("/api/mediashare/invite/", url);
        // The invite code is a 32-char GUID with no dashes
        var segments = url.Split('/');
        var inviteSegment = segments[segments.Length - 1];
        Assert.Equal(32, inviteSegment.Length);
        Assert.DoesNotContain("-", inviteSegment);
    }

    [Fact]
    public void CreateShareLink_StoresLinkInDatabase()
    {
        var url = _service.CreateShareLink("lib-1", "My Library", null);

        var segments = url.Split('/');
        var inviteCode = segments[segments.Length - 1];
        var link = _db.Links.FindOne(l => l.InviteCode == inviteCode);

        Assert.NotNull(link);
        Assert.Equal("lib-1", link.LibraryId);
    }

    [Fact]
    public void ValidateInviteCode_ReturnsNullForInvalidCode()
    {
        var result = _service.ValidateInviteCode("nonexistent_code_123456789012");

        Assert.Null(result);
    }

    [Fact]
    public void ValidateInviteCode_ReturnsNullForRevokedLink()
    {
        var url = _service.CreateShareLink("lib-1", "My Library", null);
        var inviteCode = url.Split('/').Last();
        var link = _db.Links.FindOne(l => l.InviteCode == inviteCode);
        _service.RevokeLink(link!.Id);

        var result = _service.ValidateInviteCode(inviteCode);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateInviteCode_ReturnsNullForExpiredLink()
    {
        // Create a link with an explicit expiry in the past
        var expiredLink = new Jellyfin.Plugin.MediaShare.Models.ShareLink
        {
            LibraryId = "lib-expired",
            InviteCode = "expired_code_12345678901234567",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            UsesDefaultExpiry = false
        };
        _db.Links.Insert(expiredLink);

        var result = _service.ValidateInviteCode("expired_code_12345678901234567");

        Assert.Null(result);
    }

    [Fact]
    public void ValidateInviteCode_ReturnsSharedLibraryForValidNonExpiredLink()
    {
        var url = _service.CreateShareLink("lib-1", "My Library", null);
        var inviteCode = url.Split('/').Last();

        var result = _service.ValidateInviteCode(inviteCode);

        Assert.NotNull(result);
        Assert.Equal("lib-1", result.LibraryId);
        Assert.Equal(_serverUrl, result.PeerServerUrl);
        Assert.False(result.IsIncoming);
    }

    [Fact]
    public void ValidateInviteCode_WithUsesDefaultExpiryTrue_RecomputesExpiryFromCreatedAt()
    {
        // Create a link with no explicit expiry and UsesDefaultExpiry=true (default)
        var url = _service.CreateShareLink("lib-1", "My Library", null);
        var inviteCode = url.Split('/').Last();

        // Default expiry is 604800 seconds (7 days)
        // Link was just created, so should be valid
        var result = _service.ValidateInviteCode(inviteCode, defaultExpirySeconds: 604800);

        Assert.NotNull(result);
        Assert.Equal("lib-1", result.LibraryId);
    }

    [Fact]
    public void ValidateInviteCode_WithUsesDefaultExpiryFalse_UsesStoredExpiresAt()
    {
        // Create a link via the service (guarantees correct storage)
        var url = _service.CreateShareLink("lib-explicit", "My Library", null);
        var inviteCode = url.Split('/').Last();

        // Directly update UsesDefaultExpiry to false and set a far-future ExpiresAt
        var link = _db.Links.FindOne(l => l.InviteCode == inviteCode);
        Assert.NotNull(link);
        var futureExpiry = DateTime.UtcNow.AddDays(30);
        link.UsesDefaultExpiry = false;
        link.ExpiresAt = futureExpiry;
        _db.Links.Update(link);

        // Retrieve again and verify the update persisted
        var updatedLink = _db.Links.FindById(link.Id);
        Assert.NotNull(updatedLink);
        Assert.False(updatedLink.UsesDefaultExpiry);
        Assert.NotNull(updatedLink.ExpiresAt);

        // Validate with the default short expiry — link should be valid
        // because UsesDefaultExpiry=false means the stored ExpiresAt (30 days from now)
        // is used instead of recomputing from defaultExpirySeconds
        var result = _service.ValidateInviteCode(inviteCode, defaultExpirySeconds: 1);

        Assert.NotNull(result);
        Assert.Equal("lib-explicit", result.LibraryId);
    }

    [Fact]
    public void ValidateInviteCode_WithNegativeDefaultExpirySeconds_ReturnsLibrary()
    {
        var url = _service.CreateShareLink("lib-1", "My Library", null);
        var inviteCode = url.Split('/').Last();

        // Negative defaultExpirySeconds means never expires
        var result = _service.ValidateInviteCode(inviteCode, defaultExpirySeconds: -1);

        Assert.NotNull(result);
        Assert.Equal("lib-1", result.LibraryId);
    }

    [Fact]
    public void RevokeLink_MarksIsRevokedTrue()
    {
        var url = _service.CreateShareLink("lib-1", "My Library", null);
        var inviteCode = url.Split('/').Last();
        var link = _db.Links.FindOne(l => l.InviteCode == inviteCode);
        var linkId = link!.Id;

        _service.RevokeLink(linkId);

        var revokedLink = _db.Links.FindById(linkId);
        Assert.True(revokedLink!.IsRevoked);
    }

    [Fact]
    public void GetAllActiveLinks_ExcludesRevokedLinks()
    {
        // Create and revoke one link
        var url1 = _service.CreateShareLink("lib-1", "My Library", null);
        var code1 = url1.Split('/').Last();
        var link1 = _db.Links.FindOne(l => l.InviteCode == code1);
        _service.RevokeLink(link1!.Id);

        // Create a valid link
        var url2 = _service.CreateShareLink("lib-2", "My Library 2", null);
        var code2 = url2.Split('/').Last();
        var link2 = _db.Links.FindOne(l => l.InviteCode == code2);

        var activeLinks = _service.GetAllActiveLinks().ToList();

        Assert.DoesNotContain(activeLinks, l => l.Id == link1!.Id);
        Assert.Contains(activeLinks, l => l.Id == link2!.Id);
    }

    [Fact]
    public void GetAllActiveLinks_ReturnsEmptyForAllRevoked()
    {
        var url = _service.CreateShareLink("lib-1", "My Library", null);
        var code = url.Split('/').Last();
        var link = _db.Links.FindOne(l => l.InviteCode == code);
        _service.RevokeLink(link!.Id);

        var activeLinks = _service.GetAllActiveLinks().ToList();

        Assert.Empty(activeLinks);
    }

    [Fact]
    public void GetLinksForLibrary_ReturnsLinksForSpecificLibrary()
    {
        _service.CreateShareLink("lib-1", "My Library", null);
        _service.CreateShareLink("lib-1", "My Library", null);
        _service.CreateShareLink("lib-2", "Other Library", null);

        var lib1Links = _service.GetLinksForLibrary("lib-1").ToList();

        Assert.Equal(2, lib1Links.Count);
        Assert.All(lib1Links, l => Assert.Equal("lib-1", l.LibraryId));
    }
}
