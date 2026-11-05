using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SC2ModManager.Services;
using Xunit;

namespace SC2ModManager.Tests;

public class CommunityMapRepositoryServiceTests
{
    private const string OfficialJson = """
    [
      {
        "id": "_Map_6p_Forest_v1.0.0_by_bdftm.scd",
        "fileName": "_Map_6p_Forest_v1.0.0_by_bdftm.scd",
        "author": "foxtermelon",
        "type": "Map",
        "version": "v1.0.0",
        "lastUpdatedDate": "04.11.2026",
        "downloadURL": "https://example.com/_Map_6p_Forest_v1.0.0_by_bdftm.scd",
        "playerCount": "6",
        "mapTeamStyle": "3v3",
        "size": "Medium",
        "mapName": "Forest",
        "description": "Test",
        "mapPictureFileName": "",
        "mapPictureUrl": "https://github.com/user-attachments/assets/abc",
        "tags": ["6 Players", "Team", "Medium"],
        "scenarioFilePath": "/maps/BDFTM_Forest/BDFTM_Forest_scenario.lua",
        "catalogSource": "official",
        "sourceIssueNumber": 10,
        "sourceIssueUrl": "https://github.com/maksing/SupCom2-Community-Maps/issues/10",
        "moderationStatus": "verified",
        "sha256": "ADA6FCCFBC455B398FC9786F5930FD3EB467B712684B7D97E9824204110B0046",
        "modHash": "0123456789ABCDEF0123456789ABCDEF"
      }
    ]
    """;

    private const string CommunityJson = """
    [
      {
        "id": "_Map_2p_Dune_v1.0.0_by_bdftm.scd",
        "fileName": "_Map_2p_Dune_v1.0.0_by_bdftm.scd",
        "author": "Awasy",
        "version": "v1.0.0",
        "downloadURL": "https://example.com/_Map_2p_Dune_v1.0.0_by_bdftm.scd",
        "mapName": "Dune",
        "playerCount": "",
        "mapTeamStyle": "",
        "size": "",
        "description": "",
        "tags": []
      },
      {
        "fileName": "_Map_4p_Cloudspire_v1.0.0_by_bdftm.scd",
        "mapName": "Cloudspire"
      }
    ]
    """;

    private static (CommunityMapRepositoryService Service, List<string> Requests) ServiceWithCatalogs()
    {
        var requests = new List<string>();
        var handler = new StubHandler(request =>
        {
            var url = request.RequestUri!.ToString();
            requests.Add(url);
            return url.EndsWith("/catalog/maps.json") ? OfficialJson
                : url.EndsWith("/catalog/community-maps.json") ? CommunityJson
                : null;
        });
        return (new CommunityMapRepositoryService(new HttpClient(handler)), requests);
    }

    [Fact]
    public async Task OfficialMapsAreReadFromTheCatalogBranch()
    {
        var (service, requests) = ServiceWithCatalogs();

        var official = await service.GetOfficialAsync();

        var map = Assert.Single(official);
        Assert.Equal("https://raw.githubusercontent.com/maksing/SupCom2-Community-Maps/catalog/maps.json", Assert.Single(requests));
        Assert.Equal("Forest", map.MapName);
        Assert.Equal("_Map_6p_Forest_v1.0.0_by_bdftm.scd", map.FileName);
        Assert.Equal("foxtermelon", map.Author);
        Assert.Equal("v1.0.0", map.Version);
        Assert.Equal("04.11.2026", map.LastUpdated);
        Assert.Equal("6", map.PlayerCount);
        Assert.Equal("3v3", map.MapTeamStyle);
        Assert.Equal("Medium", map.Size);
        Assert.Equal("https://github.com/user-attachments/assets/abc", map.MapPictureUrl);
        Assert.Equal("/maps/BDFTM_Forest/BDFTM_Forest_scenario.lua", map.ScenarioFilePath);
        Assert.Equal(10, map.SourceIssueNumber);
        Assert.Equal("ADA6FCCFBC455B398FC9786F5930FD3EB467B712684B7D97E9824204110B0046", map.Sha256);
        Assert.Equal("0123456789ABCDEF0123456789ABCDEF", map.ModHash);
        Assert.Equal(new[] { "6 Players", "Team", "Medium" }, map.Tags);
        Assert.Equal("official", map.CatalogSource);
        Assert.Equal("verified", map.ModerationStatus);
    }

    [Fact]
    public async Task CommunityMapsAreReadFromTheirOwnFileAndFillInMissingFields()
    {
        var (service, requests) = ServiceWithCatalogs();

        var community = await service.GetVerifiedAsync();

        Assert.Equal(2, community.Count);
        Assert.EndsWith("/catalog/community-maps.json", Assert.Single(requests));
        Assert.All(community, m => Assert.Equal("community", m.CatalogSource));
        Assert.All(community, m => Assert.Equal("verified", m.ModerationStatus));
        Assert.Equal("Dune", community[0].MapName);
        Assert.Equal(("Unknown", "Unknown", "Unknown", "No description available."),
            (community[0].PlayerCount, community[0].MapTeamStyle, community[0].Size, community[0].Description));
    }

    [Fact]
    public async Task EachFileIsDownloadedOnceWithinTheCacheTime()
    {
        var (service, requests) = ServiceWithCatalogs();

        await service.GetOfficialAsync();
        await service.GetOfficialAsync();
        await service.GetVerifiedAsync();
        await service.GetVerifiedAsync();

        Assert.Equal(2, requests.Count);
    }

    [Fact]
    public async Task CallersGetIndependentMapObjects()
    {
        var (service, _) = ServiceWithCatalogs();

        var first = (await service.GetOfficialAsync()).Single();
        first.IsDownloaded = false;
        first.FileName = "changed.scd";
        var second = (await service.GetOfficialAsync()).Single();

        Assert.True(second.IsDownloaded);
        Assert.Equal("_Map_6p_Forest_v1.0.0_by_bdftm.scd", second.FileName);
    }

    [Fact]
    public async Task AMissingCatalogFileIsAnError()
    {
        var service = new CommunityMapRepositoryService(new HttpClient(new StubHandler(_ => null)));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetOfficialAsync());
    }

    private sealed class StubHandler(Func<HttpRequestMessage, string?> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = response(request);
            return Task.FromResult(body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }
}
