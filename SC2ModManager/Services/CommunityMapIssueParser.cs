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

        var fields = FieldRegex.Matches(body)
            .ToDictionary(m => m.Groups["name"].Value.Trim(), m => Clean(m.Groups["value"].Value), StringComparer.OrdinalIgnoreCase);

        if (!fields.TryGetValue("Map name", out var name) || string.IsNullOrWhiteSpace(name) ||
            !fields.TryGetValue("Author", out var author) || string.IsNullOrWhiteSpace(author) ||
            !fields.TryGetValue("Download URL", out var url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !fields.TryGetValue("Description", out var description) || string.IsNullOrWhiteSpace(description))
            return false;

        var fileType = fields.TryGetValue("File type", out var type) ? type : string.Empty;
        if (!fileType.Equals(".scd", StringComparison.OrdinalIgnoreCase) && !fileType.Equals(".zip", StringComparison.OrdinalIgnoreCase)) return false;
        if (!uri.AbsolutePath.EndsWith(fileType, StringComparison.OrdinalIgnoreCase)) return false;

        fields.TryGetValue("Map or game version", out var version);
        fields.TryGetValue("SHA-256 (optional)", out var sha256);
        map = new Map
        {
            ID = $"community-{issueNumber}",
            FileName = Path.GetFileName(uri.AbsolutePath),
            MapName = name,
            Author = author,
            DownloadURL = url,
            Description = description,
            Version = string.IsNullOrWhiteSpace(version) ? "Unknown" : version,
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

    private static string Clean(string value) => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
}
