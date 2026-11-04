using System.Text.Json;
using System.IO;
using System.Collections.Generic;
using System;
using SC2ModManager.Models;
using Xunit;

namespace SC2ModManager.Tests;

public class OfficialCatalogRegressionTests
{
    [Fact]
    public void OfficialMapsJsonKeepsExistingContract()
    {
        var path = Environment.GetEnvironmentVariable("SC2MODS_MAPS_JSON")
            ?? Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "SC2Mods", "maps.json"));
        if (!File.Exists(path))
            path = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "SC2Mods", "maps.json"));
        if (!File.Exists(path))
            path = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", "..", "..", "..", "SC2Mods", "maps.json"));
        Assert.True(File.Exists(path), $"Official catalog fixture was not found: {path}");
        var maps = JsonSerializer.Deserialize<List<Map>>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(maps);
        Assert.NotEmpty(maps!);
        Assert.All(maps!, map =>
        {
            Assert.False(string.IsNullOrWhiteSpace(map.ID));
            Assert.False(string.IsNullOrWhiteSpace(map.FileName));
            Assert.False(string.IsNullOrWhiteSpace(map.DownloadURL));
            Assert.StartsWith("https://", map.DownloadURL);
        });
    }
}
