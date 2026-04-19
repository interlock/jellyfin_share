using Jellyfin.Plugin.MediaShare.Configuration;
using Jellyfin.Plugin.MediaShare.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.MediaShare.Controllers;

[ApiController]
[Route("mediashare")]
public class ShareLinkController : ControllerBase
{
    private readonly ShareLinkService _linkService;
    private readonly FederationService _fedService;
    private readonly long _defaultExpiry;

    public ShareLinkController(
        ShareLinkService linkService,
        FederationService fedService,
        PluginConfiguration config)
    {
        _linkService = linkService;
        _fedService = fedService;
        _defaultExpiry = config.DefaultExpirySeconds;
    }

    [HttpPost("invite/{code}")]
    public async Task<IActionResult> AcceptInvite(string code)
    {
        var library = _linkService.ValidateInviteCode(code, _defaultExpiry);
        if (library is null) return Unauthorized();

        await _fedService.SyncIncomingShareAsync(library.PeerServerUrl ?? throw new InvalidOperationException("PeerServerUrl is null"), library.PeerShareLinkId ?? code);
        return Ok(new { message = "Share accepted", libraryId = library.LibraryId });
    }

    [HttpGet("share/{linkId}/catalog")]
    public IActionResult GetCatalog(string linkId)
    {
        var link = _linkService.GetLinkById(linkId);
        if (link is null) return NotFound();

        var catalog = _fedService.BuildCatalogFromLibrary(link.LibraryId);
        return Ok(catalog);
    }

    [HttpPost("admin/links")]
    public IActionResult CreateLink([FromBody] CreateLinkRequest req)
    {
        var url = $"{Request.Scheme}://{Request.Host}";
        var inviteUrl = _linkService.CreateShareLink(req.LibraryId, req.LibraryName, req.ExpiresIn);
        return Ok(new { inviteUrl });
    }

    [HttpDelete("admin/links/{id}")]
    public IActionResult RevokeLink(string id)
    {
        _linkService.RevokeLink(id);
        return NoContent();
    }

    [HttpGet("admin/links")]
    public IActionResult GetLinks()
    {
        var links = _linkService.GetAllActiveLinks();
        return Ok(links);
    }
}

public record CreateLinkRequest(string LibraryId, string LibraryName, TimeSpan? ExpiresIn);