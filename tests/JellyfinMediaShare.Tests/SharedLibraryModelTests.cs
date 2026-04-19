using JellyfinMediaShare.Models;

namespace JellyfinMediaShare.Tests;

public class SharedLibraryModelTests
{
    [Fact]
    public void Id_DefaultsToNewGuidString()
    {
        var lib = new SharedLibrary();

        Assert.NotNull(lib.Id);
        Assert.Equal(32, lib.Id.Length);
        Assert.DoesNotContain("-", lib.Id);
        Assert.True(Guid.TryParseExact(lib.Id, "N", out _));
    }

    [Fact]
    public void LibraryId_DefaultsToEmptyString()
    {
        var lib = new SharedLibrary();

        Assert.Equal(string.Empty, lib.LibraryId);
    }

    [Fact]
    public void LibraryName_DefaultsToEmptyString()
    {
        var lib = new SharedLibrary();

        Assert.Equal(string.Empty, lib.LibraryName);
    }

    [Fact]
    public void PeerServerUrl_DefaultsToNull()
    {
        var lib = new SharedLibrary();

        Assert.Null(lib.PeerServerUrl);
    }

    [Fact]
    public void PeerShareLinkId_DefaultsToNull()
    {
        var lib = new SharedLibrary();

        Assert.Null(lib.PeerShareLinkId);
    }

    [Fact]
    public void SyncedAt_DefaultsToDateTimeUtcNow_WithinOneSecondTolerance()
    {
        var before = DateTime.UtcNow;
        var lib = new SharedLibrary();
        var after = DateTime.UtcNow;

        Assert.InRange(lib.SyncedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void IsIncoming_DefaultsToFalse()
    {
        var lib = new SharedLibrary();

        Assert.False(lib.IsIncoming);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        var lib = new SharedLibrary
        {
            LibraryId = "remote-lib-123",
            LibraryName = "Remote Movies",
            PeerServerUrl = "http://remote-server:8096",
            PeerShareLinkId = "link-abc",
            SyncedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            IsIncoming = true
        };

        Assert.Equal("remote-lib-123", lib.LibraryId);
        Assert.Equal("Remote Movies", lib.LibraryName);
        Assert.Equal("http://remote-server:8096", lib.PeerServerUrl);
        Assert.Equal("link-abc", lib.PeerShareLinkId);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), lib.SyncedAt);
        Assert.True(lib.IsIncoming);
    }
}
