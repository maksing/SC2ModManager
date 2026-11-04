using System.Net;
using System.Net.Http;
using System.Text;
using System;
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
          {"number":1,"html_url":"https://github.com/maksing/SupCom2-Community-Maps/issues/1","body":"### Map name\n\nMap One\n\n### Author\n\nA\n\n### Download URL\n\nhttps://example.com/map.scd\n\n### File type\n\n.scd\n\n### Map or game version\n\n1\n\n### Description\n\nTest"},
          {"number":2,"html_url":"https://github.com/maksing/SupCom2-Community-Maps/pull/2","body":"ignored","pull_request":{}}
        ]
        """);
        var service = new CommunityMapRepositoryService(new HttpClient(handler));

        var maps = await service.GetVerifiedAsync();

        var map = Assert.Single(maps);
        Assert.Equal(1, map.SourceIssueNumber);
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
