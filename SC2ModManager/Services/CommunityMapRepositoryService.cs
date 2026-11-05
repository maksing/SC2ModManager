using SC2ModManager.Models;
using System.Net.Http;
using System.Text.Json;

namespace SC2ModManager.Services;

/// <summary>
///     Reads the map catalogs that the community maps repository publishes on its catalog branch:
///     maps.json for the official maps (verified and promoted issues) and community-maps.json for the community maps
///     (verified issues that are not promoted). Hidden maps are not in either file.
/// </summary>
public sealed class CommunityMapRepositoryService
{
    private const string OfficialFile = "maps.json";
    private const string CommunityFile = "community-maps.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>One instance for the whole app so each file is downloaded once and shared by everything that needs it.</summary>
    public static CommunityMapRepositoryService Shared { get; } = new();

    private readonly HttpClient http;
    private readonly string repo;
    private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim fetchLock = new(1, 1);
    private readonly Dictionary<string, (string Json, DateTime FetchedAt)> cache = new();

    public CommunityMapRepositoryService(HttpClient? httpClient = null, string repository = "maksing/SupCom2-Community-Maps")
    {
        http = httpClient ?? new HttpClient();
        repo = repository;
        if (!http.DefaultRequestHeaders.UserAgent.Any()) http.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager/1.6");
        http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>The community maps catalog.</summary>
    public async Task<List<Map>> GetVerifiedAsync(CancellationToken token = default) =>
        await GetCatalogAsync(CommunityFile, "community", token);

    /// <summary>The official maps catalog.</summary>
    public async Task<List<Map>> GetOfficialAsync(CancellationToken token = default) =>
        await GetCatalogAsync(OfficialFile, "official", token);

    private async Task<List<Map>> GetCatalogAsync(string file, string catalogSource, CancellationToken token)
    {
        var json = await GetJsonAsync(file, token);
        // Deserializing the cached text gives every caller its own Map objects, so one catalog changing a map
        // (IsDownloaded, FileName) never affects another.
        var maps = JsonSerializer.Deserialize<List<Map>>(json, JsonOptions) ?? new List<Map>();
        foreach (var map in maps)
        {
            map.CatalogSource = catalogSource;
            map.ModerationStatus = "verified";
            // The catalog files use empty strings for optional values the submitter left out.
            if (string.IsNullOrWhiteSpace(map.PlayerCount)) map.PlayerCount = "Unknown";
            if (string.IsNullOrWhiteSpace(map.MapTeamStyle)) map.MapTeamStyle = "Unknown";
            if (string.IsNullOrWhiteSpace(map.Size)) map.Size = "Unknown";
            if (string.IsNullOrWhiteSpace(map.Description)) map.Description = "No description available.";
        }
        return maps;
    }

    private async Task<string> GetJsonAsync(string file, CancellationToken token)
    {
        await fetchLock.WaitAsync(token);
        try
        {
            if (cache.TryGetValue(file, out var cached) && DateTime.UtcNow - cached.FetchedAt < cacheDuration)
                return cached.Json;

            var url = $"https://raw.githubusercontent.com/{repo}/catalog/{file}";
            var json = await http.GetStringAsync(url, token);
            cache[file] = (json, DateTime.UtcNow);
            return json;
        }
        finally
        {
            fetchLock.Release();
        }
    }
}
