using SC2ModManager.Services;
using Xunit;

namespace SC2ModManager.Tests;

public class CommunityMapIssueParserTests
{
    // Normalized so the replacements below work whichever line endings the file is checked out with.
    private static readonly string Body = """
### Form version

- [x] I am submitting through the SC2CommunityMaps form v1.

### Map name

Desert Arena

### Author

Maksing

### Download URL

https://example.com/desert-arena.zip

### File type

.zip

### Map or mappack version

1.0.0

### Scenario File Path

/maps/desert_arena/desert_arena_scenario.lua

### Description

2v2 community map

### SHA-256 (generated automatically)

abc123
""".ReplaceLineEndings("\n");

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
        Assert.Equal("/maps/desert_arena/desert_arena_scenario.lua", map.ScenarioFilePath);
    }

    [Theory]
    [InlineData("http://example.com/map.zip")]
    [InlineData("https://example.com/map.exe")]
    public void RejectsUnsafeOrUnsupportedUrl(string url)
    {
        var body = Body.Replace("https://example.com/desert-arena.zip", url);
        Assert.False(CommunityMapIssueParser.TryParse(body, 1, "", "pending", out _));
    }

    [Theory]
    [InlineData("1.0")]
    [InlineData("1.0.0.1")]
    [InlineData("v1.0.0")]
    public void RejectsInvalidVersion(string version)
    {
        var body = Body.Replace("1.0.0", version);
        Assert.False(CommunityMapIssueParser.TryParse(body, 1, "", "pending", out _));
    }

    [Fact]
    public void ParsesMultipleScenarioPaths()
    {
        var body = Body
            .Replace("### Scenario File Path", "### Scenario File Paths")
            .Replace("/maps/desert_arena/desert_arena_scenario.lua", "/maps/desert_arena/desert_arena_scenario.lua;/maps/desert_arena/desert_arena_preview.lua");
        Assert.True(CommunityMapIssueParser.TryParse(body, 1, "", "pending", out var map));
        Assert.Contains("desert_arena_scenario.lua", map.ScenarioFilePath);
        Assert.Contains("desert_arena_preview.lua", map.ScenarioFilePath);
    }

    [Fact]
    public void ParsesIssueWithoutFileTypeAndWithScenarioPathsOnSeparateLines()
    {
        var body = Body
            .Replace("### File type\n\n.zip\n\n", "")
            .Replace("### Scenario File Path", "### Scenario File Paths")
            .Replace("/maps/desert_arena/desert_arena_scenario.lua", "/maps/desert_arena/desert_arena_scenario.lua\n/maps/desert_arena/desert_arena_preview.lua");
        Assert.True(CommunityMapIssueParser.TryParse(body, 1, "", "pending", out var map));
        Assert.Equal("desert-arena.zip", map.FileName);
        Assert.Equal(2, map.ScenarioFilePath.Split(System.Environment.NewLine).Length);
    }

    [Fact]
    public void RejectsFileTypeThatDoesNotMatchUrl()
    {
        var body = Body.Replace("### File type\n\n.zip", "### File type\n\n.scd");
        Assert.False(CommunityMapIssueParser.TryParse(body, 1, "", "pending", out _));
    }

    [Fact]
    public void RejectsScenarioPathWithoutLuaExtension()
    {
        var body = Body.Replace("/maps/desert_arena/desert_arena_scenario.lua", "/maps/desert_arena/desert_arena_scenario.txt");
        Assert.False(CommunityMapIssueParser.TryParse(body, 1, "", "pending", out _));
    }

    [Fact]
    public void RejectsMissingRequiredField()
    {
        Assert.False(CommunityMapIssueParser.TryParse(Body.Replace("### Author\n\nMaksing", ""), 1, "", "pending", out _));
    }

    [Fact]
    public void OptionalOfficialMetadataAndImageAreParsed()
    {
        var body = Body
            .Replace("### Map or mappack version\n\n1.0.0", "### Map or mappack version\n\n1.0.0")
            .Replace("### Description\n\n2v2 community map", "### Description\n\n_No response_\n\n### Players\n\n2 Players\n\n### Style\n\nTeam\n\n### Size\n\nLarge\n\n### Map image\n\n![Map preview](https://github.com/user-attachments/assets/map-preview)")
            .Replace("### SHA-256 (generated automatically)", "### Tags\n\n2 Players, Team\n\n### SHA-256 (generated automatically)");

        Assert.True(CommunityMapIssueParser.TryParse(body, 7, "", "verified", out var map));
        Assert.Equal("1.0.0", map.Version);
        Assert.Equal("No description available.", map.Description);
        Assert.Equal("2 Players", map.PlayerCount);
        Assert.Equal("Team", map.MapTeamStyle);
        Assert.Equal("Large", map.Size);
        Assert.Equal("https://github.com/user-attachments/assets/map-preview", map.MapPictureUrl);
        Assert.Contains("Team", map.Tags);
    }

    [Fact]
    public void ParsesIssueWithoutFormVersionCheckbox()
    {
        Assert.True(CommunityMapIssueParser.TryParse(Body.Replace("### Form version\n\n- [x] I am submitting through the SC2CommunityMaps form v1.\n\n", ""), 1, "", "verified", out _));
    }

    [Fact]
    public void RejectsFreeTextIssueThatIsNotTheForm()
    {
        Assert.False(CommunityMapIssueParser.TryParse("Please add my map, it is great.", 1, "", "verified", out _));
    }
}
