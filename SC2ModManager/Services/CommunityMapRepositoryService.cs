using SC2ModManager.Models;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SC2ModManager.Services;

/// <summary>
///     Reads map submissions from the community maps repository's GitHub issues.
///     verified + promoted issues are the official catalog, verified issues without promoted are the community
///     catalog, and any verified issue that also has the hidden label is not shown anywhere.
/// </summary>
public sealed class CommunityMapRepositoryService
{
    private const string VerifiedLabel = "verified";
    private const string PromotedLabel = "promoted";
    private const string HiddenLabel = "hidden";

    /// <summary>One instance for the whole app so the verified issues are fetched once and shared by both catalogs.</summary>
    public static CommunityMapRepositoryService Shared { get; } = new();

    private readonly HttpClient http;
    private readonly string repo;
    private readonly TimeSpan cacheDuration = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim fetchLock = new(1, 1);
    private List<LabeledMap>? verifiedCache;
    private DateTime verifiedFetchedAt = DateTime.MinValue;

    public CommunityMapRepositoryService(HttpClient? httpClient = null, string repository = "maksing/SupCom2-Community-Maps")
    {
        http = httpClient ?? new HttpClient();
        repo = repository;
        if (!http.DefaultRequestHeaders.UserAgent.Any()) http.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager/1.6");
        http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>Verified, not promoted and not hidden: the community maps catalog.</summary>
    public async Task<List<Map>> GetVerifiedAsync(CancellationToken token = default) =>
        (await GetVerifiedIssuesAsync(token))
            .Where(m => !m.Labels.Contains(PromotedLabel) && !m.Labels.Contains(HiddenLabel))
            .Select(m => m.Map)
            .ToList();

    /// <summary>Verified and promoted, not hidden: the official maps catalog.</summary>
    public async Task<List<Map>> GetOfficialAsync(CancellationToken token = default) =>
        (await GetVerifiedIssuesAsync(token))
            .Where(m => m.Labels.Contains(PromotedLabel) && !m.Labels.Contains(HiddenLabel))
            .Select(m =>
            {
                m.Map.CatalogSource = "official";
                return m.Map;
            })
            .ToList();

    public async Task<List<Map>> GetPendingAsync(CancellationToken token = default) =>
        (await FetchIssuesAsync("pending", token)).Select(m => m.Map).ToList();

    private async Task<List<LabeledMap>> GetVerifiedIssuesAsync(CancellationToken token)
    {
        await fetchLock.WaitAsync(token);
        try
        {
            if (verifiedCache is null || DateTime.UtcNow - verifiedFetchedAt >= cacheDuration)
            {
                verifiedCache = await FetchIssuesAsync(VerifiedLabel, token);
                verifiedFetchedAt = DateTime.UtcNow;
            }
            // Hand out copies so one catalog changing a map (IsDownloaded, CatalogSource) never affects the other.
            return verifiedCache.Select(m => new LabeledMap(CloneMap(m.Map), m.Labels)).ToList();
        }
        finally
        {
            fetchLock.Release();
        }
    }

    private static Map CloneMap(Map map) => JsonSerializer.Deserialize<Map>(JsonSerializer.Serialize(map))!;

    private async Task<List<LabeledMap>> FetchIssuesAsync(string label, CancellationToken token)
    {
        var result = new List<LabeledMap>();
        for (var page = 1; page <= 20; page++)
        {
            var url = $"https://api.github.com/repos/{repo}/issues?state=open&labels={Uri.EscapeDataString(label)}&per_page=100&page={page}";
            using var response = await http.GetAsync(url, token);
            if (response.StatusCode is HttpStatusCode.Forbidden or (HttpStatusCode)429)
                throw new HttpRequestException($"GitHub API rate limit: {(int)response.StatusCode}");
            response.EnsureSuccessStatusCode();
            var items = await response.Content.ReadFromJsonAsync<List<IssueDto>>(cancellationToken: token) ?? new();
            if (items.Count == 0) break;
            foreach (var item in items)
            {
                if (item.PullRequest is not null) continue;
                if (!CommunityMapIssueParser.TryParse(item.Body ?? string.Empty, item.Number, item.HtmlUrl, label, out var map)) continue;
                var labels = (item.Labels ?? new()).Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                result.Add(new LabeledMap(map, labels));
            }
            if (items.Count < 100) break;
        }
        return result;
    }

    private sealed record LabeledMap(Map Map, HashSet<string> Labels);

    private sealed class IssueDto
    {
        public int Number { get; set; }
        public string? Body { get; set; }
        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = string.Empty;
        [JsonPropertyName("pull_request")]
        public JsonElement? PullRequest { get; set; }
        public List<LabelDto>? Labels { get; set; }
    }

    private sealed class LabelDto
    {
        public string Name { get; set; } = string.Empty;
    }
}
