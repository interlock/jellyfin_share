using Jellyfin.Plugin.MediaShare.Configuration;
using Jellyfin.Plugin.MediaShare.Services;

namespace Jellyfin.Plugin.MediaShare.Tests;

public class ShareLinkServiceTests
{
    private PluginConfiguration MakeConfig()
        => new PluginConfiguration { DefaultExpirySeconds = 604800 };

    [Fact]
    public void CreateShareLink_ReturnsNonEmptyUrl()
    {
        var config = MakeConfig();
        var svc = new ShareLinkService(
            () => config,
            c => { },
            "http://localhost:8096");

        var url = svc.CreateShareLink("lib-1", "My Library", null, 604800);

        Assert.NotNull(url);
        Assert.NotEmpty(url);
    }

    [Fact]
    public void CreateShareLink_ReturnsUrlContainingInviteCode()
    {
        var config = MakeConfig();
        var svc = new ShareLinkService(
            () => config,
            c => { },
            "http://localhost:8096");

        var url = svc.CreateShareLink("lib-1", "My Library", null, 604800);

        Assert.Contains("/api/mediashare/invite/", url);
        var segments = url.Split('/');
        var code = segments[^1];
        Assert.Equal(32, code.Length);
        Assert.DoesNotContain("-", code);
    }

    [Fact]
    public void CreateShareLink_StoresLinkInConfig()
    {
        var config = MakeConfig();
        var saved = false;
        var svc = new ShareLinkService(
            () => config,
            c => { saved = true; },
            "http://localhost:8096");

        var url = svc.CreateShareLink("lib-1", "My Library", null, 604800);
        var code = url.Split('/')[^1];

        Assert.Single(config.SharedLibraries);
        Assert.Equal("lib-1", config.SharedLibraries[0].LibraryId);
        Assert.Single(config.SharedLibraries[0].Links);
        Assert.Equal(code, config.SharedLibraries[0].Links[0].Id);
    }

    [Fact]
    public void ValidateInviteCode_ReturnsNullForInvalidCode()
    {
        var config = MakeConfig();
        var svc = new ShareLinkService(() => config, c => { }, "http://localhost:8096");

        var result = svc.ValidateInviteCode("nonexistent_code_12345678901234567", 604800);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateInviteCode_ReturnsNullForExpiredLink()
    {
        var config = MakeConfig();
        config.SharedLibraries.Add(new LibraryShare
        {
            LibraryId = "lib-expired",
            Links =
            [
                new ShareLinkInfo
                {
                    Id = "expired_code_12345678901234567",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
                    UsesDefaultExpiry = false
                }
            ]
        });
        var svc = new ShareLinkService(() => config, c => { }, "http://localhost:8096");

        var result = svc.ValidateInviteCode("expired_code_12345678901234567", 604800);

        Assert.Null(result);
    }

    [Fact]
    public void ValidateInviteCode_ReturnsLinkForValidNonExpiredLink()
    {
        var config = MakeConfig();
        config.SharedLibraries.Add(new LibraryShare
        {
            LibraryId = "lib-1",
            Links = [new ShareLinkInfo { Id = "valid_code_12345678901234567890", UsesDefaultExpiry = false }]
        });
        var svc = new ShareLinkService(() => config, c => { }, "http://localhost:8096");

        var result = svc.ValidateInviteCode("valid_code_12345678901234567890", 604800);

        Assert.NotNull(result);
        Assert.Equal("valid_code_12345678901234567890", result.Id);
    }

    [Fact]
    public void ValidateInviteCode_UsesDefaultExpiry_WhenLinkUsesDefault()
    {
        var config = MakeConfig();
        config.SharedLibraries.Add(new LibraryShare
        {
            LibraryId = "lib-1",
            Links = [new ShareLinkInfo { Id = "default_expiry_code_1234567890", UsesDefaultExpiry = true }]
        });
        var svc = new ShareLinkService(() => config, c => { }, "http://localhost:8096");

        // Just-created link with default expiry — should be valid
        var result = svc.ValidateInviteCode("default_expiry_code_1234567890", 604800);

        Assert.NotNull(result);
    }

    [Fact]
    public void ValidateInviteCode_NeverExpires_WhenDefaultIsNegative()
    {
        var config = MakeConfig();
        config.SharedLibraries.Add(new LibraryShare
        {
            LibraryId = "lib-1",
            Links = [new ShareLinkInfo { Id = "never_expires_12345678901234567", UsesDefaultExpiry = true }]
        });
        var svc = new ShareLinkService(() => config, c => { }, "http://localhost:8096");

        var result = svc.ValidateInviteCode("never_expires_12345678901234567", -1);

        Assert.NotNull(result);
    }

    [Fact]
    public void RevokeLink_MarksLinkAsRevoked()
    {
        var config = MakeConfig();
        config.SharedLibraries.Add(new LibraryShare
        {
            LibraryId = "lib-1",
            Links = [new ShareLinkInfo { Id = "revoke_this_12345678901234567" }]
        });
        var saved = false;
        var svc = new ShareLinkService(() => config, c => { saved = true; }, "http://localhost:8096");

        svc.RevokeLink("revoke_this_12345678901234567");

        Assert.True(config.SharedLibraries[0].Links[0].IsRevoked);
        Assert.True(saved);
    }

    [Fact]
    public void GetAllActiveLinks_ExcludesRevoked()
    {
        var config = MakeConfig();
        config.SharedLibraries.Add(new LibraryShare
        {
            LibraryId = "lib-1",
            Links =
            [
                new ShareLinkInfo { Id = "link1_12345678901234567890", IsRevoked = true },
                new ShareLinkInfo { Id = "link2_12345678901234567890", IsRevoked = false }
            ]
        });
        var svc = new ShareLinkService(() => config, c => { }, "http://localhost:8096");

        var active = svc.GetAllActiveLinks().ToList();

        Assert.Single(active);
        Assert.Equal("link2_12345678901234567890", active[0].Id);
    }

    [Fact]
    public void GetAllActiveLinks_ReturnsEmptyWhenAllRevoked()
    {
        var config = MakeConfig();
        config.SharedLibraries.Add(new LibraryShare
        {
            LibraryId = "lib-1",
            Links = [new ShareLinkInfo { Id = "revoked_12345678901234567890", IsRevoked = true }]
        });
        var svc = new ShareLinkService(() => config, c => { }, "http://localhost:8096");

        var active = svc.GetAllActiveLinks().ToList();

        Assert.Empty(active);
    }

    [Fact]
    public void CreateShareLink_AddsToExistingLibraryShare()
    {
        var config = MakeConfig();
        config.SharedLibraries.Add(new LibraryShare { LibraryId = "lib-1", LibraryName = "My Library" });
        var svc = new ShareLinkService(() => config, c => { }, "http://localhost:8096");

        svc.CreateShareLink("lib-1", "My Library", null, 604800);
        svc.CreateShareLink("lib-1", "My Library", null, 604800);

        Assert.Single(config.SharedLibraries);
        Assert.Equal(2, config.SharedLibraries[0].Links.Count);
    }

    [Fact]
    public void CreateShareLink_SetsExpiryFromTimeSpan()
    {
        var config = MakeConfig();
        var svc = new ShareLinkService(() => config, c => { }, "http://localhost:8096");

        svc.CreateShareLink("lib-1", "My Library", TimeSpan.FromHours(1), 604800);

        var link = config.SharedLibraries[0].Links[0];
        Assert.False(link.UsesDefaultExpiry);
        Assert.NotNull(link.ExpiresAt);
    }
}