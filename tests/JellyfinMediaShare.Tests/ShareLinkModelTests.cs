using Jellyfin.Plugin.MediaShare.Models;

namespace Jellyfin.Plugin.MediaShare.Tests;

public class ShareLinkModelTests
{
    [Fact]
    public void Id_DefaultsToNewGuidString()
    {
        var link = new ShareLink();

        Assert.NotNull(link.Id);
        Assert.Equal(32, link.Id.Length);
        Assert.DoesNotContain("-", link.Id);
        Assert.True(Guid.TryParseExact(link.Id, "N", out _));
    }

    [Fact]
    public void CreatedAt_DefaultsToDateTimeUtcNow_WithinOneSecondTolerance()
    {
        var before = DateTime.UtcNow;
        var link = new ShareLink();
        var after = DateTime.UtcNow;

        Assert.InRange(link.CreatedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void ExpiresAt_CanBeNull()
    {
        var link = new ShareLink
        {
            LibraryId = "lib-1",
            ExpiresAt = null
        };

        Assert.Null(link.ExpiresAt);
    }

    [Fact]
    public void ExpiresAt_CanBeSetToFutureDate()
    {
        var future = DateTime.UtcNow.AddDays(7);
        var link = new ShareLink
        {
            LibraryId = "lib-1",
            ExpiresAt = future
        };

        Assert.NotNull(link.ExpiresAt);
        Assert.Equal(future, link.ExpiresAt);
    }

    [Fact]
    public void UsesDefaultExpiry_DefaultsToTrue()
    {
        var link = new ShareLink();

        Assert.True(link.UsesDefaultExpiry);
    }

    [Fact]
    public void IsRevoked_DefaultsToFalse()
    {
        var link = new ShareLink();

        Assert.False(link.IsRevoked);
    }

    [Fact]
    public void LibraryId_DefaultsToEmptyString()
    {
        var link = new ShareLink();

        Assert.Equal(string.Empty, link.LibraryId);
    }

    [Fact]
    public void InviteCode_DefaultsToNull()
    {
        var link = new ShareLink();

        Assert.Null(link.InviteCode);
    }
}
