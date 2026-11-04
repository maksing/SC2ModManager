using System.Net;
using System.Net.Http;
using System.Text;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SC2ModManager.Services;
using Xunit;

namespace SC2ModManager.Tests;

public class CommunityMapRepositoryServiceTests
{
    [Fact]
    public async Task FiltersPullRequestsAndParsesIssues()
    {
        var handler = new StubHandler(_ => """
        [
          {"number":99,"html_url":"https://github.com/maksing/SupCom2-Community-Maps/issues/99","body":"### Form version\n\n- [x] I am submitting through the SC2CommunityMaps form v1.\n\n### Map name\n\nMap One\n\n### Author\n\nA\n\n### Download URL\n\nhttps://example.com/map.scd\n\n### File type\n\n.scd\n\n### Map or mappack version\n\n1.0.0\n\n### Scenario File Path\n\n/maps/map_one/map_one_scenario.lua\n\n### Description\n\nTest\n\n### SHA-256 (generated automatically)\n\n0000000000000000000000000000000000000000000000000000000000000000"},
          {"number":1,"html_url":"https://github.com/maksing/SupCom2-Community-Maps/issues/1","body":"Please add my map, it is great."},
          {"number":2,"html_url":"https://github.com/maksing/SupCom2-Community-Maps/pull/2","body":"ignored","pull_request":{}}
        ]
        """);
        var service = new CommunityMapRepositoryService(new HttpClient(handler));

        var maps = await service.GetVerifiedAsync();

        var map = Assert.Single(maps);
        Assert.Equal(99, map.SourceIssueNumber);
    }

    private static string IssueJson(int number, params string[] labels)
    {
        var body = string.Join("\n\n", "### Map name", $"Map {number}", "### Author", "A", "### Download URL", $"https://example.com/map{number}.scd", "### Map or mappack version", "1.0.0", "### Scenario File Paths", $"/maps/m{number}/m{number}_scenario.lua");
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            number,
            html_url = $"https://github.com/maksing/SupCom2-Community-Maps/issues/{number}",
            body,
            labels = labels.Select(l => new { name = l }).ToArray()
        });
    }

    private static CommunityMapRepositoryService ServiceWith(params string[] issues) =>
        new(new HttpClient(new StubHandler(_ => "[" + string.Join(",", issues) + "]")));

    [Fact]
    public async Task OfficialMapsAreVerifiedAndPromotedWithoutHidden()
    {
        var service = ServiceWith(
            IssueJson(1, "verified"),
            IssueJson(2, "verified", "promoted"),
            IssueJson(3, "verified", "promoted", "hidden"));

        var official = await service.GetOfficialAsync();

        var map = Assert.Single(official);
        Assert.Equal(2, map.SourceIssueNumber);
        Assert.Equal("official", map.CatalogSource);
    }

    [Fact]
    public async Task CommunityMapsAreVerifiedWithoutPromotedOrHidden()
    {
        var service = ServiceWith(
            IssueJson(1, "verified"),
            IssueJson(2, "verified", "promoted"),
            IssueJson(3, "verified", "hidden"));

        var community = await service.GetVerifiedAsync();

        var map = Assert.Single(community);
        Assert.Equal(1, map.SourceIssueNumber);
        Assert.Equal("community", map.CatalogSource);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, string> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response(request), Encoding.UTF8, "application/json")
            });
    }
}
