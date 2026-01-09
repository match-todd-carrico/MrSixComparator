using System.Collections.Concurrent;
using Serilog;
using Spectre.Console;
using MrSixResultsComparator.Models;
using MrSixResultsComparator.Configuration;
using MrSixResultsComparator.Helpers;

namespace MrSixResultsComparator.Services;

public class ComparisonService
{
    private readonly AppConfiguration _config;
    private readonly StackSearchService _stackSearchService;
    private readonly ConcurrentBag<ComparisonResult> _comparisonResults;

    public ComparisonService(AppConfiguration config, StackSearchService stackSearchService)
    {
        _config = config;
        _stackSearchService = stackSearchService;
        _comparisonResults = new ConcurrentBag<ComparisonResult>();
    }

    public async Task CompareSearchResults(List<SearchParameter> searchParameters)
    {
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Comparing StackSearch Results[/]");
                task.MaxValue = searchParameters.Count();

                await Parallel.ForEachAsync(
                    searchParameters, 
                    new ParallelOptions { MaxDegreeOfParallelism = _config.MaxParallelism }, 
                    async (searchParam, ct) =>
                    {
                        try
                        {
                            await CompareSearchParameter(searchParam);
                        }
                        catch (Exception ex)
                        {
                            AnsiConsole.MarkupLine($"[yellow]ERROR:[/] Failed to process search - SearcherUserId: {searchParam.SearcherUserId}: {ex.Message}");
                            Log.Error(ex, "Failed to process search. SiteCode: {SiteCode}, SearcherUserId: {SearcherUserId}, CallId: {CallId}",
                                searchParam.SiteCode, searchParam.SearcherUserId, searchParam.CallId);
                        }
                        finally
                        {
                            task.Increment(1);
                        }
                    });
            });

        AnsiConsole.MarkupLine("[bold green]StackSearch Comparison Complete![/]");
        Console.WriteLine();

        DisplaySummary();
    }

    private async Task CompareSearchParameter(SearchParameter searchParam)
    {
        Log.Information("Starting comparison for {Description}", searchParam.Description);
        
        // 1. Execute StackSearch on both environments
        var resultA = await _stackSearchService.ExecuteStackSearch(searchParam, _config.MrSixControl);
        var resultB = await _stackSearchService.ExecuteStackSearch(searchParam, _config.MrSixTest);

        // 2. Extract UserIds from results
        var userIdsA = _stackSearchService.ExtractUserIds(resultA);
        var userIdsB = _stackSearchService.ExtractUserIds(resultB);

        // 3. Compare UserIds
        var onlyInA = userIdsA.Except(userIdsB).ToList();
        var onlyInB = userIdsB.Except(userIdsA).ToList();
        var inBoth = userIdsA.Intersect(userIdsB).ToList();
        
        bool hasDifferences = onlyInA.Any() || onlyInB.Any() || userIdsA.Count != userIdsB.Count;

        if (hasDifferences)
        {
            RecordDifference(searchParam, userIdsA, userIdsB, onlyInA, onlyInB, inBoth);
        }
        else
        {
            RecordMatch(searchParam, userIdsA.Count);
        }
    }

    private void RecordDifference(
        SearchParameter searchParam, 
        List<int> userIdsA, 
        List<int> userIdsB,
        List<int> onlyInA,
        List<int> onlyInB,
        List<int> inBoth)
    {
        // Track this difference
        _comparisonResults.Add(new ComparisonResult
        {
            SiteCode = searchParam.SiteCode,
            SearcherUserId = searchParam.SearcherUserId,
            Matched = false
        });
        
        // Display focused comparison
        OutputHelper.DisplayDifference(searchParam, userIdsA.Count, userIdsB.Count, onlyInA, onlyInB, inBoth);
        
        // Log the difference
        Log.Warning("Difference found for SearcherUserId: {SearcherUserId}, SiteCode: {SiteCode}, CallId: {CallId}",
            searchParam.SearcherUserId, searchParam.SiteCode, searchParam.CallId);
        Log.Warning("Control count: {ControlCount}, Test count: {TestCount}", userIdsA.Count, userIdsB.Count);
        Log.Warning("Only in Control: {OnlyInControl}", string.Join(",", onlyInA));
        Log.Warning("Only in Test: {OnlyInTest}", string.Join(",", onlyInB));
        Log.Information("Control UserIds: {ControlUserIds}", string.Join(",", userIdsA));
        Log.Information("Test UserIds: {TestUserIds}", string.Join(",", userIdsB));
    }

    private void RecordMatch(SearchParameter searchParam, int resultCount)
    {
        // Track this match
        _comparisonResults.Add(new ComparisonResult
        {
            SiteCode = searchParam.SiteCode,
            SearcherUserId = searchParam.SearcherUserId,
            Matched = true
        });
        
        AnsiConsole.MarkupLine($"[green]✓[/] Match - SearcherUserId: {searchParam.SearcherUserId} ({resultCount} results)");
        Log.Information("Results match for SearcherUserId: {SearcherUserId} ({Count} results)", 
            searchParam.SearcherUserId, resultCount);
    }

    private void DisplaySummary()
    {
        var results = _comparisonResults.ToList();
        SummaryHelper.DisplaySummary(results);
    }
}
