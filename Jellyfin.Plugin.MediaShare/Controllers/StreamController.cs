using Jellyfin.Plugin.MediaShare.Configuration;
using Microsoft.AspNetCore.Mvc;
using IOPath = System.IO;

namespace Jellyfin.Plugin.MediaShare.Controllers;

public class StreamController : ControllerBase
{
    private readonly Func<PluginConfiguration> _getConfig;

    public StreamController(Func<PluginConfiguration> getConfig)
    {
        _getConfig = getConfig;
    }

    [HttpGet("stream/{shareId}/{fileId}")]
    public IActionResult Stream(string shareId, string fileId)
    {
        var decoded = Uri.UnescapeDataString(fileId);
        var parts = decoded.Split("::", 2);
        if (parts.Length < 2) return NotFound("Invalid file ID");

        var incoming = _getConfig().IncomingShares.FirstOrDefault(s => s.Id == shareId);
        if (incoming is null) return NotFound("Share not found");

        var localPath = incoming.FileMap.GetValueOrDefault(parts[1]);
        if (string.IsNullOrEmpty(localPath) || !IOPath.File.Exists(localPath)) return NotFound("File not found");

        var totalBytes = new IOPath.FileInfo(localPath).Length;
        Response.Headers.AcceptRanges = "bytes";

        if (Request.Headers.Range.Count > 0)
        {
            var (start, end) = ParseRange(Request.Headers.Range[0]!, totalBytes);
            Response.ContentType = "video/mp4";
            Response.Headers.ContentRange = $"{start}-{end}/{totalBytes}";
            Response.StatusCode = 206;
            Response.ContentLength = end - start + 1;

            using var fs = new IOPath.FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Seek(start, SeekOrigin.Begin);
            fs.CopyTo(Response.Body);
        }
        else
        {
            Response.ContentType = "video/mp4";
            Response.ContentLength = totalBytes;
            var fs = new IOPath.FileStream(localPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new FileStreamResult(fs, "video/mp4");
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
}