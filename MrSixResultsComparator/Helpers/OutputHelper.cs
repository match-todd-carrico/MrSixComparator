using Spectre.Console;
using MrSixResultsComparator.Core.Models;

namespace MrSixResultsComparator.Helpers;

public static class OutputHelper
{
    public static void DisplayDifference(ComparisonResult result)
    {
        AnsiConsole.MarkupLine($"[red]═══ DIFF FOUND ═══[/]");
        AnsiConsole.MarkupLine($"[yellow]SearcherUserId:[/] {result.SearcherUserId}");
        AnsiConsole.MarkupLine($"[yellow]SiteCode:[/] {result.SiteCode} | [yellow]CallId:[/] {result.CallId}");
        AnsiConsole.MarkupLine($"[yellow]CallTime:[/] {result.CallTime}");
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine($"[cyan]Control Result Count:[/] {result.ControlCount}");
        AnsiConsole.MarkupLine($"[cyan]Test Result Count:[/] {result.TestCount}");
        AnsiConsole.WriteLine();
        
        if (result.OnlyInControl.Any())
        {
            AnsiConsole.MarkupLine($"[red]UserIds only in Control ({result.OnlyInControl.Count}):[/]");
            AnsiConsole.MarkupLine($"  {string.Join(", ", result.OnlyInControl)}");
            AnsiConsole.WriteLine();
        }
        
        if (result.OnlyInTest.Any())
        {
            AnsiConsole.MarkupLine($"[blue]UserIds only in Test ({result.OnlyInTest.Count}):[/]");
            AnsiConsole.MarkupLine($"  {string.Join(", ", result.OnlyInTest)}");
            AnsiConsole.WriteLine();
        }
        
        if (result.InBoth.Any())
        {
            AnsiConsole.MarkupLine($"[green]UserIds in both ({result.InBoth.Count}):[/]");
            AnsiConsole.MarkupLine($"  {string.Join(", ", result.InBoth)}");
            AnsiConsole.WriteLine();
        }
        
        Console.WriteLine(new string('═', 60));
        Console.WriteLine();
    }

    public static void DisplayMatch(ComparisonResult result)
    {
        AnsiConsole.MarkupLine($"[green]✓[/] Match - SearcherUserId: {result.SearcherUserId} ({result.ControlCount} results)");
    }
}
