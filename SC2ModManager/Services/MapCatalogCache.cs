using SC2ModManager.Models;
using System.IO;
using System.Text.Json;

namespace SC2ModManager.Services;

public sealed class MapCatalogCache
{
    private readonly string path;
    private DateTime lastRefresh = DateTime.MinValue;
    private readonly TimeSpan refreshInterval = TimeSpan.FromMinutes(10);

    public MapCatalogCache()
    {
        path = Path.Combine(Globals.GetDataPath(), "community_maps_cache.json");
    }

    public bool CanRefresh => DateTime.UtcNow - lastRefresh >= refreshInterval;

    public async Task SaveAsync(IEnumerable<Map> maps)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(maps));
        lastRefresh = DateTime.UtcNow;
    }

    public async Task<List<Map>> LoadAsync()
    {
        if (!File.Exists(path)) return new();
        try { return JsonSerializer.Deserialize<List<Map>>(await File.ReadAllTextAsync(path)) ?? new(); }
        catch { return new(); }
    }
}
