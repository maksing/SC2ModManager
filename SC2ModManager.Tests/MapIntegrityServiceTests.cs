using SC2ModManager.Models;
using SC2ModManager.Services;
using System.Threading.Tasks;
using System.IO;
using Xunit;

namespace SC2ModManager.Tests;

public class MapIntegrityServiceTests
{
    [Fact]
    public async Task Sha256MismatchDeletesFileAndFailsValidation()
    {
        var directory = Directory.CreateTempSubdirectory("sc2-map-integrity-");
        try
        {
            var path = Path.Combine(directory.FullName, "map.scd");
            await File.WriteAllTextAsync(path, "actual map bytes");
            var map = new Map("map.scd") { Sha256 = new string('0', 64) };

            await Assert.ThrowsAsync<IOException>(() => MapIntegrityService.ValidateSha256Async(map, path));

            Assert.False(File.Exists(path));
        }
        finally
        {
            directory.Delete(true);
        }
    }
}
