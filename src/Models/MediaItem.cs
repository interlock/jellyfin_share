namespace JellyfinMediaShare.Models;

public class MediaItem
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int? Year { get; set; }
    public string MediaType { get; set; } = string.Empty; // Movie, Series, Episode, Audio
    public List<MediaFile> Files { get; set; } = [];
    public string? PosterUrl { get; set; }
    public string? Overview { get; set; }
}

public class MediaFile
{
    public string Id { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
}