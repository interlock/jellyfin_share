using System.Net.Http.Headers;
using Jellyfin.Plugin.MediaShare.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.MediaShare.Controllers;

[ApiController]
[Route("mediashare")]
public class StreamController(
    ShareLinkService linkService,
    IHttpClientFactory httpClientFactory,
    ILogger<StreamController> logger) : ControllerBase
{
    private readonly HttpClient _http = httpClientFactory.CreateClient("Jellyfin.Plugin.MediaShare");

    [HttpGet("stream/{linkId}/{fileId}")]
    public async Task<IActionResult> Stream(string linkId, string fileId)
    {
        var link = linkService.GetLinkById(linkId);
        if (link is null) return NotFound();

        var (peerUrl, itemId) = ParseFileId(fileId, link);
        if (peerUrl is null) return NotFound("File not found");

        var resp = await _http.GetAsync($"{peerUrl}/mediashare/file/{itemId}", HttpCompletionOption.ResponseHeadersRead);
        if (!resp.IsSuccessStatusCode) return StatusCode((int)resp.StatusCode);

        var contentType = resp.Content.Headers.ContentType?.MediaType ?? "video/*";
        var totalBytes = resp.Content.Headers.ContentLength;

        Response.Headers.AcceptRanges = "bytes";
        if (Request.Headers.Range.Count > 0)
        {
            var (start, end) = ParseRange(Request.Headers.Range[0], totalBytes ?? 0);
            Response.Headers.ContentRange = $"{start}-{end}/{totalBytes ?? 0}";
            Response.StatusCode = 206;
            var stream = await _http.GetStreamAsync(new Uri($"{peerUrl}/mediashare/file/{itemId}"));
            await stream.CopyToAsync(Response.Body);
        }
        else
        {
            Response.Headers.ContentLength = totalBytes;
            return File(resp.Content.ReadAsStream(), contentType);
        }

        return new EmptyResult();
    }

    private static (long start, long end) ParseRange(string range, long total)
    {
        var parts = range.Replace("bytes=", "").Split('-');
        var start = parts[0].Length > 0 ? long.Parse(parts[0]) : 0;
        var end = parts[1].Length > 0 ? long.Parse(parts[1]) : total - 1;
        return (start, Math.Min(end, total - 1));
    }

    private static (string? peerUrl, string? itemId) ParseFileId(string fileId, Models.ShareLink link)
    {
        // fileId format: "{peerServerUrl}::{itemId}"
        var idx = fileId.IndexOf("::");
        if (idx < 0) return (null, null);
        return (fileId[..idx], fileId[(idx + 2)..]);
    }
}