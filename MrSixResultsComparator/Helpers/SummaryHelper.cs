using Spectre.Console;
using Serilog;
using MrSixResultsComparator.Core.Models;

namespace MrSixResultsComparator.Helpers;

public static class SummaryHelper
{
    public static void DisplaySummary(List<ComparisonResult> results)
    {
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════[/]");
        AnsiConsole.MarkupLine("[bold cyan]           COMPARISON SUMMARY BY SITECODE      [/]");
        AnsiConsole.MarkupLine("[bold cyan]═══════════════════════════════════════════════[/]");
        Console.WriteLine();

        var groupedBySite = results.GroupBy(r => r.SiteCode).OrderBy(g => g.Key);

        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn(new TableColumn("[yellow]SiteCode[/]").Centered());
        table.AddColumn(new TableColumn("[cyan]Total Searches[/]").Centered());
        table.AddColumn(new TableColumn("[green]Matched[/]").Centered());
        table.AddColumn(new TableColumn("[red]Mismatched[/]").Centered());
        table.AddColumn(new TableColumn("[red]Mismatched SearcherUserIds[/]").LeftAligned());

        foreach (var siteGroup in groupedBySite)
        {
            var total = siteGroup.Count();
            var matched = siteGroup.Count(r => r.Matched);
            var mismatched = siteGroup.Count(r => !r.Matched);
            var mismatchedUserIds = siteGroup.Where(r => !r.Matched).Select(r => r.SearcherUserId).OrderBy(id => id);
            
            var mismatchedIdsDisplay = mismatched > 0 
                ? string.Join(", ", mismatchedUserIds)
                : "-";

            var matchedColor = matched == total ? "green" : "white";
            var mismatchedColor = mismatched > 0 ? "red" : "green";

            table.AddRow(
                $"[yellow]{siteGroup.Key}[/]",
                $"[cyan]{total}[/]",
                $"[{matchedColor}]{matched}[/]",
                $"[{mismatchedColor}]{mismatched}[/]",
                mismatched > 0 ? $"[red]{mismatchedIdsDisplay}[/]" : $"[green]{mismatchedIdsDisplay}[/]"
            );

            // Log summary data
            Log.Information("Summary for SiteCode {SiteCode}: Total={Total}, Matched={Matched}, Mismatched={Mismatched}, MismatchedUserIds={MismatchedUserIds}",
                siteGroup.Key, total, matched, mismatched, mismatchedIdsDisplay);
        }

        AnsiConsole.Write(table);
        Console.WriteLine();

        // Overall summary
        DisplayOverallSummary(results);
    }

    private static void DisplayOverallSummary(List<ComparisonResult> results)
    {
        var totalSearches = results.Count;
        var totalMatched = results.Count(r => r.Matched);
        var totalMismatched = results.Count(r => !r.Matched);
        var successRate = totalSearches > 0 ? (totalMatched * 100.0 / totalSearches) : 0;

        AnsiConsole.MarkupLine($"[bold]Overall Results:[/]");
        AnsiConsole.MarkupLine($"  Total Searches: [cyan]{totalSearches}[/]");
        AnsiConsole.MarkupLine($"  Matched: [green]{totalMatched}[/]");
        AnsiConsole.MarkupLine($"  Mismatched: [red]{totalMismatched}[/]");
        AnsiConsole.MarkupLine($"  Success Rate: [{(successRate == 100 ? "green" : "yellow")}]{successRate:F1}%[/]");
        Console.WriteLine();

        Log.Information("Overall Summary: Total={Total}, Matched={Matched}, Mismatched={Mismatched}, SuccessRate={SuccessRate:F1}%",
            totalSearches, totalMatched, totalMismatched, successRate);
    }
}
