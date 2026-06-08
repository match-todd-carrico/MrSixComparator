using System.Text.RegularExpressions;

namespace MrSixResultsComparator.BlazorApp.Data;

/// <summary>
/// Parses the pools.txt slice of an MrSix explain into named pools with ordered userIds.
/// Each pool starts with a banner block of the form:
///   ===...===
///   =    {PoolName}, Rows: N (where Target was T, Available: A)    =
///   ===...===
/// Each row line ends with "UserId: NNN." and is associated with the most recently
/// opened pool. File order is preserved as the pool's reading order — the parser does
/// not interpret OrderNum, so a user higher in the file is at a lower index.
///
/// Tolerant by design: garbage / truncated lines that still contain "UserId: NNN" are
/// captured under the current pool; lines that don't match either pattern are ignored.
/// </summary>
public static class PoolsExplainParser
{
    private static readonly Regex HeaderBannerRegex = new(
        @"^=\s+(?<name>\S+)\s*,\s+Rows:\s+\d+",
        RegexOptions.Compiled);

    private static readonly Regex UserRowRegex = new(
        @"UserId:\s+(?<id>\d+)",
        RegexOptions.Compiled);

    public static List<PoolBlock> Parse(string? content)
    {
        var pools = new List<PoolBlock>();
        if (string.IsNullOrWhiteSpace(content)) return pools;

        PoolBlock? current = null;
        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            var banner = HeaderBannerRegex.Match(line);
            if (banner.Success)
            {
                current = new PoolBlock { Name = banner.Groups["name"].Value };
                pools.Add(current);
                continue;
            }

            var user = UserRowRegex.Match(line);
            if (user.Success && current is not null && int.TryParse(user.Groups["id"].Value, out var id))
            {
                current.UserIds.Add(id);
            }
        }
        return pools;
    }
}

public sealed class PoolBlock
{
    public string Name { get; set; } = "";
    public List<int> UserIds { get; } = new();
}
