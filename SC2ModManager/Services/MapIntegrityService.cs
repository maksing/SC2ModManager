using SC2ModManager.Models;
using System.IO;
using System.Security.Cryptography;

namespace SC2ModManager.Services;

public static class MapIntegrityService
{
    public static async Task ValidateSha256Async(Map map, string path)
    {
        if (string.IsNullOrWhiteSpace(map.Sha256)) return;

        byte[] hash;
        await using (var stream = File.OpenRead(path))
        {
            hash = await SHA256.HashDataAsync(stream);
        }
        var actual = Convert.ToHexString(hash);
        if (actual.Equals(map.Sha256.Trim(), StringComparison.OrdinalIgnoreCase)) return;

        File.Delete(path);
        throw new IOException("SHA-256 checksum does not match the submitted map.");
    }
}
