using System.Text;
using MrSixResultsComparator.Core.Configuration;
using MrSixResultsComparator.Core.Models;

namespace MrSixResultsComparator.BlazorApp.Data;

public static class AiContextBuilder
{
    public static string BuildPrompt(
        ComparisonResult result,
        AppConfiguration config,
        string? controlExplain,
        string? testExplain)
    {
        var sb = new StringBuilder();

        sb.AppendLine("## MrSix Search Result Comparison — Analysis Request");
        sb.AppendLine();
        sb.AppendLine("### Search Parameters");
        sb.AppendLine($"- **SiteCode:** {result.SiteCode}");
        sb.AppendLine($"- **SearcherUserId:** {result.SearcherUserId}");
        sb.AppendLine($"- **SearchService:** {result.SearchServiceName}");
        sb.AppendLine($"- **Control Server:** {config.MrSixControl}");
        sb.AppendLine($"- **Test Server:** {config.MrSixTest}");
        sb.AppendLine($"- **Control CallId:** {result.ControlCallId}");
        sb.AppendLine($"- **Test CallId:** {result.TestCallId}");
        sb.AppendLine($"- **Matched (set-based):** {result.Matched}");
        if (result.WasRetried)
            sb.AppendLine($"- **Retry Matched:** {result.RetryMatched}");
        sb.AppendLine();

        // Set differences
        sb.AppendLine("### Set Differences");
        sb.AppendLine($"- **Control result count:** {result.ControlCount}");
        sb.AppendLine($"- **Test result count:** {result.TestCount}");

        if (result.OnlyInControl.Any())
            sb.AppendLine($"- **Only in Control ({result.OnlyInControl.Count}):** {FormatUserIds(result.OnlyInControl)}");
        if (result.OnlyInTest.Any())
            sb.AppendLine($"- **Only in Test ({result.OnlyInTest.Count}):** {FormatUserIds(result.OnlyInTest)}");
        sb.AppendLine($"- **In Both:** {result.InBoth.Count}");

        if (result.IgnoredFromControl.Any() || result.IgnoredFromTest.Any())
        {
            sb.AppendLine();
            sb.AppendLine("#### Ignored (Data Movement / Eventual Consistency)");
            if (result.IgnoredFromControl.Any())
                sb.AppendLine($"- From Control: {FormatUserIds(result.IgnoredFromControl)}");
            if (result.IgnoredFromTest.Any())
                sb.AppendLine($"- From Test: {FormatUserIds(result.IgnoredFromTest)}");
        }

        // Slot type breakdown
        if (result.OnlyInControlBySlotType.Any() || result.OnlyInTestBySlotType.Any())
        {
            sb.AppendLine();
            sb.AppendLine("#### Set Differences by ResultSlotType (AlgoId)");
            foreach (var kvp in result.OnlyInControlBySlotType.OrderByDescending(x => x.Value.Count))
                sb.AppendLine($"- Only in Control / SlotType {kvp.Key}: {FormatUserIds(kvp.Value)}");
            foreach (var kvp in result.OnlyInTestBySlotType.OrderByDescending(x => x.Value.Count))
                sb.AppendLine($"- Only in Test / SlotType {kvp.Key}: {FormatUserIds(kvp.Value)}");
        }

        sb.AppendLine();

        // Property differences
        if (result.PropertyDifferences.Any())
        {
            sb.AppendLine("### Property Differences (users present in both result sets)");
            sb.AppendLine();
            sb.AppendLine("| UserId | Property | Control | Test |");
            sb.AppendLine("|--------|----------|---------|------|");

            foreach (var diff in result.PropertyDifferences.OrderBy(d => d.UserId).ThenBy(d => d.PropertyName))
            {
                sb.AppendLine($"| {diff.UserId} | {diff.PropertyName} | {diff.ControlValue} | {diff.TestValue} |");
            }

            sb.AppendLine();

            var affectedUsers = result.PropertyDifferences.Select(d => d.UserId).Distinct().Count();
            var affectedProperties = result.PropertyDifferences.Select(d => d.PropertyName).Distinct().OrderBy(p => p);
            sb.AppendLine($"*{result.PropertyDifferences.Count} differences across {affectedUsers} users. Properties affected: {string.Join(", ", affectedProperties)}*");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("### Property Differences");
            sb.AppendLine("No property-level differences detected for users present in both result sets.");
            sb.AppendLine();
        }

        // Explains
        sb.AppendLine("### Control Server Explain");
        sb.AppendLine("```");
        sb.AppendLine(controlExplain ?? "Not available — explains are generated during retry runs with explain enabled.");
        sb.AppendLine("```");
        sb.AppendLine();

        sb.AppendLine("### Test Server Explain");
        sb.AppendLine("```");
        sb.AppendLine(testExplain ?? "Not available — explains are generated during retry runs with explain enabled.");
        sb.AppendLine("```");
        sb.AppendLine();

        // Guiding prompt
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("Analyze why these search results differ between the Control and Test servers. Focus on:");
        sb.AppendLine("1. What caused users to appear in one result set but not the other (set differences)");
        sb.AppendLine("2. Why scoring, ranking, or ordering changed for users present in both sets (property differences)");
        sb.AppendLine("3. Any configuration or flag differences visible in the explains (e.g., Polaris Test, HasAdvancedSeekCriteria, filtering changes)");
        sb.AppendLine("4. Whether these differences indicate a code regression or an expected behavior change");

        return sb.ToString();
    }

    private static string FormatUserIds(List<int> userIds, int max = 50)
    {
        if (userIds.Count <= max)
            return string.Join(", ", userIds);

        return string.Join(", ", userIds.Take(max)) + $" ... ({userIds.Count - max} more)";
    }
}
