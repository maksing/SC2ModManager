using SC2ModManager.Models;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace SC2ModManager.Services;

public sealed class CommunityMapRepositoryService
{
    private readonly HttpClient http;
    private readonly string repo;

    public CommunityMapRepositoryService(HttpClient? httpClient = null, string repository = "maksing/SupCom2-Community-Maps")
    {
        http = httpClient ?? new HttpClient();
        repo = repository;
        if (!http.DefaultRequestHeaders.UserAgent.Any()) http.DefaultRequestHeaders.UserAgent.ParseAdd("SC2ModManager/1.6");
        http.Timeout = TimeSpan.FromSeconds(30);
    }

    public Task<List<Map>> GetVerifiedAsync(CancellationToken token = default) => GetAsync("verified", token);
    public Task<List<Map>> GetPendingAsync(CancellationToken token = default) => GetAsync("pending", token);

    private async Task<List<Map>> GetAsync(string label, CancellationToken token)
    {
        var result = new List<Map>();
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
                if (CommunityMapIssueParser.TryParse(item.Body ?? string.Empty, item.Number, item.HtmlUrl, label, out var map)) result.Add(map);
            }
            if (items.Count < 100) break;
        }
        return result;
    }

    private sealed class IssueDto
    {
        public int Number { get; set; }
        public string? Body { get; set; }
        public string HtmlUrl { get; set; } = string.Empty;
        public JsonElement? PullRequest { get; set; }
    }
}
