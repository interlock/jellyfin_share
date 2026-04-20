using Jellyfin.Plugin.MediaShare.Configuration;
using Jellyfin.Plugin.MediaShare.Services;
using MediaBrowser.Common.Plugins;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MediaShare.Controllers;

[ApiController]
[Route("mediashare")]
public class ShareLinkController : ControllerBase
{
    private readonly IPluginManager _pluginManager;

    public ShareLinkController(IPluginManager pluginManager)
    {
        _pluginManager = pluginManager;
    }

    private Plugin PluginInstance
        => (Plugin)((LocalPlugin)_pluginManager.GetPlugin(Plugin.PluginId)!).Instance;

    private ShareLinkService LinkService => PluginInstance.LinkService;
    private FederationService FedService => PluginInstance.FedService;
    private Func<PluginConfiguration> GetConfig => () => PluginInstance.Configuration;
    private Action<PluginConfiguration> SaveConfig => cfg => PluginInstance.SetConfiguration(cfg);

    [HttpPost("invite/{code}")]
    public async Task<IActionResult> AcceptInvite(string code)
    {
        var link = LinkService.ValidateInviteCode(code, defaultExpirySeconds: 604800);
        if (link is null) return Unauthorized();

        await FedService.AcceptInviteAsync(link.InviteUrl);
        return Ok(new { message = "Share accepted" });
    }

    [HttpGet("share/{linkId}/catalog")]
    public IActionResult GetCatalog(string linkId)
    {
        var link = LinkService.GetLinkById(linkId);
        if (link is null) return NotFound();
        return Ok(FedService.BuildCatalogFromLibrary(link.LibraryId.Length > 0 ? link.LibraryId : link.Id));
    }

    [HttpPost("admin/links")]
    public IActionResult CreateLink([FromBody] CreateLinkRequest req)
    {
        TimeSpan? expiresIn = req.ExpiresInSeconds switch
        {
            null => null,
            -1 => null,
            _ => TimeSpan.FromSeconds(req.ExpiresInSeconds!.Value)
        };

        var inviteUrl = LinkService.CreateShareLink(
            req.LibraryId,
            req.LibraryName,
            expiresIn,
            defaultExpirySeconds: 604800);
        return Ok(new { inviteUrl });
    }

    [HttpDelete("admin/links/{id}")]
    public IActionResult RevokeLink(string id)
    {
        LinkService.RevokeLink(id);
        return NoContent();
    }

    [HttpGet("admin/links")]
    public IActionResult GetLinks()
    {
        return Ok(LinkService.GetAllActiveLinks());
    }

    [HttpPost("admin/shares/accept")]
    public async Task<IActionResult> AcceptShare([FromBody] AcceptShareRequest req)
    {
        await FedService.AcceptInviteAsync(req.InviteUrl);
        return Ok(new { message = "Share accepted" });
    }

    [HttpDelete("admin/shares/{id}")]
    public IActionResult RemoveIncomingShare(string id)
    {
        var config = GetConfig();
        var share = config.IncomingShares.FirstOrDefault(s => s.Id == id);
        if (share is null) return NotFound();

        config.IncomingShares.Remove(share);
        SaveConfig(config);
        return NoContent();
    }

    [HttpGet("admin/shares")]
    public IActionResult GetIncomingShares()
    {
        return Ok(GetConfig().IncomingShares);
    }

    [HttpPut("admin/settings")]
    public IActionResult UpdateSettings([FromBody] UpdateSettingsRequest req)
    {
        var config = GetConfig();
        config.WatchFolderPath = req.WatchFolderPath;
        config.DefaultExpirySeconds = req.DefaultExpirySeconds;
        SaveConfig(config);
        return Ok(new { message = "Settings saved" });
    }
}

public record AcceptShareRequest(string InviteUrl);
public record UpdateSettingsRequest(string? WatchFolderPath, long DefaultExpirySeconds);
public record CreateLinkRequest(string LibraryId, string LibraryName, long? ExpiresInSeconds);
