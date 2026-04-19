using JellyfinMediaShare.Models;
using LiteDB;

namespace JellyfinMediaShare.Data;

public class ShareDbContext : IDisposable
{
    private readonly LiteDatabase _db;

    public ShareDbContext(string dataPath)
    {
        _db = new LiteDatabase(dataPath);
        Links = _db.GetCollection<ShareLink>("share_links");
        Libraries = _db.GetCollection<SharedLibrary>("shared_libraries");
    }

    public ILiteCollection<ShareLink> Links { get; }
    public ILiteCollection<SharedLibrary> Libraries { get; }

    public void Dispose() => _db.Dispose();
}