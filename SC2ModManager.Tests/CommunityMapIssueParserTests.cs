using SC2ModManager.Services;
using Xunit;

namespace SC2ModManager.Tests;

public class CommunityMapIssueParserTests
{
    private const string Body = """
### Map name

Desert Arena

### Author

Maksing

### Download URL

https://example.com/desert-arena.zip

### File type

.zip

### Map or game version

1.0

### Description

2v2 community map

### SHA-256 (generated automatically)

abc123
""";

    [Fact]
    public void ParsesValidIssueForm()
    {
        Assert.True(CommunityMapIssueParser.TryParse(Body, 42, "https://github.com/maksing/SupCom2-Community-Maps/issues/42", "verified", out var map));
        Assert.Equal("Desert Arena", map.MapName);
        Assert.Equal("desert-arena.zip", map.FileName);
        Assert.Equal("community", map.CatalogSource);
        Assert.Equal(42, map.SourceIssueNumber);
        Assert.Equal("verified", map.ModerationStatus);
        Assert.Equal("abc123", map.Sha256);
    }

    [Theory]
    [InlineData("http://example.com/map.zip")]
    [InlineData("https://example.com/map.exe")]
    public void RejectsUnsafeOrUnsupportedUrl(string url)
    {
        var body = Body.Replace("https://example.com/desert-arena.zip", url);
        Assert.False(CommunityMapIssueParser.TryParse(body, 1, "", "pending", out _));
    }

    [Fact]
    public void RejectsMissingRequiredField()
    {
        Assert.False(CommunityMapIssueParser.TryParse(Body.Replace("### Description\n\n2v2 community map", ""), 1, "", "pending", out _));
    }
}
