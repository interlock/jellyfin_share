using Jellyfin.Plugin.MediaShare.Configuration;
using Jellyfin.Plugin.MediaShare.Models;

namespace Jellyfin.Plugin.MediaShare.Services;

public class ShareLinkService(
    Func<PluginConfiguration> getConfig,
    Action<PluginConfiguration> saveConfig,
    string serverUrl)
{
    public string CreateShareLink(string libraryId, string libraryName, TimeSpan? expiresIn, long defaultExpirySeconds)
    {
        var config = getConfig();

        var share = config.SharedLibraries.FirstOrDefault(s => s.LibraryId == libraryId);
        if (share is null)
        {
            share = new LibraryShare { LibraryId = libraryId, LibraryName = libraryName };
            config.SharedLibraries.Add(share);
        }

        var code = Guid.NewGuid().ToString("N");
        var info = new ShareLinkInfo
        {
            Id = code,
            LibraryId = libraryId,
            CreatedAt = DateTime.UtcNow,
            UsesDefaultExpiry = !expiresIn.HasValue,
            ExpiresAt = expiresIn.HasValue
                ? DateTime.UtcNow.Add(expiresIn.Value)
                : (defaultExpirySeconds < 0 ? null : DateTime.UtcNow.AddSeconds(defaultExpirySeconds)),
            InviteUrl = $"{serverUrl}/api/mediashare/invite/{code}"
        };

        share.Links.Add(info);
        saveConfig(config);
        return info.InviteUrl;
    }

    public ShareLinkInfo? ValidateInviteCode(string code, long defaultExpirySeconds)
    {
        var config = getConfig();
        foreach (var share in config.SharedLibraries)
        {
            var link = share.Links.FirstOrDefault(l => l.Id == code && !l.IsRevoked);
            if (link is null) continue;

            var effectiveExpiry = link.ExpiresAt;
            if (link.UsesDefaultExpiry)
            {
                effectiveExpiry = defaultExpirySeconds < 0
                    ? (DateTime?)null
                    : DateTime.UtcNow.AddSeconds(defaultExpirySeconds);
            }

            if (effectiveExpiry.HasValue && effectiveExpiry < DateTime.UtcNow) return null;
            return link;
        }
        return null;
    }

    public ShareLinkInfo? GetLinkById(string id)
    {
        var config = getConfig();
        foreach (var share in config.SharedLibraries)
        {
            var link = share.Links.FirstOrDefault(l => l.Id == id && !l.IsRevoked);
            if (link is not null) return link;
        }
        return null;
    }

    public void RevokeLink(string id)
    {
        var config = getConfig();
        foreach (var share in config.SharedLibraries)
        {
            var link = share.Links.FirstOrDefault(l => l.Id == id);
            if (link is not null)
            {
                link.IsRevoked = true;
                saveConfig(config);
                return;
            }
        }
    }

    public IEnumerable<ShareLinkInfo> GetAllActiveLinks()
    {
        var config = getConfig();
        return config.SharedLibraries
            .SelectMany(s => s.Links)
            .Where(l => !l.IsRevoked)
            .ToList();
    }
}