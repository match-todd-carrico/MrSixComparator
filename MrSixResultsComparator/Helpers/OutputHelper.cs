using Spectre.Console;
using MrSixResultsComparator.Models;

namespace MrSixResultsComparator.Helpers;

public static class OutputHelper
{
    public static void DisplayDifference(
        SearchParameter searchParam,
        int controlCount,
        int testCount,
        List<int> onlyInControl,
        List<int> onlyInTest,
        List<int> inBoth)
    {
        AnsiConsole.MarkupLine($"[red]═══ DIFF FOUND ═══[/]");
        AnsiConsole.MarkupLine($"[yellow]SearcherUserId:[/] {searchParam.SearcherUserId}");
        AnsiConsole.MarkupLine($"[yellow]SiteCode:[/] {searchParam.SiteCode} | [yellow]CallId:[/] {searchParam.CallId}");
        AnsiConsole.MarkupLine($"[yellow]CallTime:[/] {searchParam.CallTime}");
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine($"[cyan]Control Result Count:[/] {controlCount}");
        AnsiConsole.MarkupLine($"[cyan]Test Result Count:[/] {testCount}");
        AnsiConsole.WriteLine();
        
        if (onlyInControl.Any())
        {
            AnsiConsole.MarkupLine($"[red]UserIds only in Control ({onlyInControl.Count}):[/]");
            AnsiConsole.MarkupLine($"  {string.Join(", ", onlyInControl)}");
            AnsiConsole.WriteLine();
        }
        
        if (onlyInTest.Any())
        {
            AnsiConsole.MarkupLine($"[blue]UserIds only in Test ({onlyInTest.Count}):[/]");
            AnsiConsole.MarkupLine($"  {string.Join(", ", onlyInTest)}");
            AnsiConsole.WriteLine();
        }
        
        if (inBoth.Any())
        {
            AnsiConsole.MarkupLine($"[green]UserIds in both ({inBoth.Count}):[/]");
            AnsiConsole.MarkupLine($"  {string.Join(", ", inBoth)}");
            AnsiConsole.WriteLine();
        }
        
        Console.WriteLine(new string('═', 60));
        Console.WriteLine();
    }
}
