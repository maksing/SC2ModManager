using SC2ModManager.Models;
using System.IO;
using System.Text.RegularExpressions;

namespace SC2ModManager.Services;

public static class CommunityMapIssueParser
{
    private static readonly Regex FieldRegex = new(
        @"^###\s+(?<name>[^\r\n]+)\s*\r?\n(?<value>(?:(?!^###\s).)*?)(?=^###\s|\z)",
        RegexOptions.Multiline | RegexOptions.Singleline | RegexOptions.Compiled);

    public static bool TryParse(string body, int issueNumber, string issueUrl, string status, out Map map)
    {
        map = null;
        if (string.IsNullOrWhiteSpace(body)) return false;
        if (!body.Contains("I am submitting through the SC2CommunityMaps form v1.", StringComparison.Ordinal)) return false;

        var fields = FieldRegex.Matches(body)
            .ToDictionary(m => m.Groups["name"].Value.Trim(), m => Clean(m.Groups["value"].Value), StringComparer.OrdinalIgnoreCase);

        if (!fields.TryGetValue("Map name", out var name) || string.IsNullOrWhiteSpace(name) ||
            !fields.TryGetValue("Author", out var author) || string.IsNullOrWhiteSpace(author) ||
            !fields.TryGetValue("Download URL", out var url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;

        var fileType = fields.TryGetValue("File type", out var type) ? type : string.Empty;
        if (!fileType.Equals(".scd", StringComparison.OrdinalIgnoreCase) && !fileType.Equals(".zip", StringComparison.OrdinalIgnoreCase)) return false;
        if (!uri.AbsolutePath.EndsWith(fileType, StringComparison.OrdinalIgnoreCase)) return false;

        var version = Optional(fields, "Map or game version", "Unknown");
        var description = Optional(fields, "Description", "No description available.");
        var players = Optional(fields, "Players", "Unknown");
        var style = Optional(fields, "Style", "Unknown");
        var size = Optional(fields, "Size", "Unknown");
        var imageUrl = Optional(fields, "Map image URL", string.Empty);
        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var imageUri) ||
             !string.Equals(imageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            imageUrl = string.Empty;

        var tags = Optional(fields, "Tags", string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();
        if (!fields.TryGetValue("SHA-256 (generated automatically)", out var sha256))
            fields.TryGetValue("SHA-256 (optional)", out sha256);
        if (string.Equals(sha256, "_No response_", StringComparison.OrdinalIgnoreCase)) sha256 = string.Empty;
        map = new Map
        {
            ID = $"community-{issueNumber}",
            FileName = Path.GetFileName(uri.AbsolutePath),
            MapName = name,
            Author = author,
            DownloadURL = url,
            Description = description,
            Version = version,
            PlayerCount = players,
            MapTeamStyle = style,
            Size = size,
            MapPictureUrlOverride = imageUrl,
            Tags = tags,
            CatalogSource = "community",
            SourceIssueNumber = issueNumber,
            SourceIssueUrl = issueUrl ?? string.Empty,
            ModerationStatus = status,
            Sha256 = sha256 ?? string.Empty,
            LastUpdated = DateTime.UtcNow.ToString("O")
        };
        if (string.IsNullOrWhiteSpace(map.FileName) || !map.FileName.EndsWith(fileType, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }

    private static string Optional(IReadOnlyDictionary<string, string> fields, string name, string fallback) =>
        fields.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) && !value.Equals("_No response_", StringComparison.OrdinalIgnoreCase)
            ? value
            : fallback;

    private static string Clean(string value) => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
}
