using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.MediaShare.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public List<LibraryShare> SharedLibraries { get; set; } = [];
    public List<IncomingShare> IncomingShares { get; set; } = [];
    public string? WatchFolderPath { get; set; }

    /// Default expiry for newly created invite links. Negative = never, otherwise seconds.
    public long DefaultExpirySeconds { get; set; } = 604800; // 7 days
}

public class LibraryShare
{
    public string LibraryId { get; set; } = string.Empty;
    public string LibraryName { get; set; } = string.Empty;
    public List<ShareLinkInfo> Links { get; set; } = [];
}

public class ShareLinkInfo
{
    public string Id { get; set; } = string.Empty;
    public string LibraryId { get; set; } = string.Empty;
    public string InviteUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool UsesDefaultExpiry { get; set; } = true;
    public bool IsRevoked { get; set; }
}

public class IncomingShare
{
    public string Id { get; set; } = string.Empty;
    public string PeerServerUrl { get; set; } = string.Empty;
    public string? PeerShareLinkId { get; set; }
    public string LibraryName { get; set; } = string.Empty;
    public DateTime AddedAt { get; set; }
    public DateTime SyncedAt { get; set; }
    public int ItemCount { get; set; }
    /// Maps peer file paths to local .strm file paths
    public Dictionary<string, string> FileMap { get; set; } = [];
}