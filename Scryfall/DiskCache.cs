using System.Security.Cryptography;
using System.Text;

namespace MtgDeckCli.Scryfall;

public sealed class DiskCache
{
    private readonly string _root;

    public DiskCache(string root)
    {
        _root = root;
        Directory.CreateDirectory(_root);
    }

    public async Task<string?> TryGetAsync(string key, TimeSpan maxAge)
    {
        var path = PathFor(key);
        if (!File.Exists(path)) return null;

        var info = new FileInfo(path);
        if (DateTime.UtcNow - info.LastWriteTimeUtc > maxAge) return null;

        return await File.ReadAllTextAsync(path);
    }

    public async Task PutAsync(string key, string value)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, value);
    }

    private string PathFor(string key)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(key));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        // shard dirs to avoid huge single folders
        var dir = Path.Combine(_root, hex[..2], hex[2..4]);
        return Path.Combine(dir, $"{hex}.json");
    }
}