using System.Collections.Concurrent;
using Serilog;
using MrSixResultsComparator.Core.Models;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.Core.Services;

public class ComparisonProgressEventArgs : EventArgs
{
    public int Current { get; set; }
    public int Total { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ComparisonResultEventArgs : EventArgs
{
    public ComparisonResult Result { get; set; } = null!;
}

public class ComparisonService
{
    private readonly AppConfiguration _config;
    private readonly StackSearchService _stackSearchService;
    private readonly ConcurrentBag<ComparisonResult> _comparisonResults;

    public event EventHandler<ComparisonProgressEventArgs>? ProgressUpdated;
    public event EventHandler<ComparisonResultEventArgs>? ComparisonCompleted;
    public event EventHandler<ComparisonResultEventArgs>? DifferenceFound;

    public IReadOnlyList<ComparisonResult> Results => _comparisonResults.ToList();

    public ComparisonService(AppConfiguration config, StackSearchService stackSearchService)
    {
        _config = config;
        _stackSearchService = stackSearchService;
        _comparisonResults = new ConcurrentBag<ComparisonResult>();
    }

    public async Task CompareSearchResults(List<SearchParameter> searchParameters)
    {
        int total = searchParameters.Count;
        int current = 0;

        OnProgressUpdated(new ComparisonProgressEventArgs 
        { 
            Current = 0, 
            Total = total, 
            Message = "Starting comparison..." 
        });

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
                    Log.Error(ex, "Failed to process search. SiteCode: {SiteCode}, SearcherUserId: {SearcherUserId}, CallId: {CallId}",
                        searchParam.SiteCode, searchParam.SearcherUserId, searchParam.CallId);
                }
                finally
                {
                    current++;
                    OnProgressUpdated(new ComparisonProgressEventArgs 
                    { 
                        Current = current, 
                        Total = total, 
                        Message = $"Processed {current} of {total} searches..." 
                    });
                }
            });

        OnProgressUpdated(new ComparisonProgressEventArgs 
        { 
            Current = total, 
            Total = total, 
            Message = "Comparison complete!" 
        });
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
        var result = new ComparisonResult
        {
            SiteCode = searchParam.SiteCode,
            SearcherUserId = searchParam.SearcherUserId,
            Matched = false,
            ControlCount = userIdsA.Count,
            TestCount = userIdsB.Count,
            OnlyInControl = onlyInA,
            OnlyInTest = onlyInB,
            InBoth = inBoth,
            CallId = searchParam.CallId,
            CallTime = searchParam.CallTime
        };
        
        _comparisonResults.Add(result);
        OnDifferenceFound(new ComparisonResultEventArgs { Result = result });
        OnComparisonCompleted(new ComparisonResultEventArgs { Result = result });
        
        // Log the difference
        Log.Warning("Difference found for SearcherUserId: {SearcherUserId}, SiteCode: {SiteCode}, CallId: {CallId}",
            searchParam.SearcherUserId, searchParam.SiteCode, searchParam.CallId);
        Log.Warning("Control count: {ControlCount}, Test count: {TestCount}", userIdsA.Count, userIdsB.Count);
        Log.Warning("Only in Control: {OnlyInControl}", string.Join(",", onlyInA));
        Log.Warning("Only in Test: {OnlyInTest}", string.Join(",", onlyInB));
    }

    private void RecordMatch(SearchParameter searchParam, int resultCount)
    {
        // Track this match
        var result = new ComparisonResult
        {
            SiteCode = searchParam.SiteCode,
            SearcherUserId = searchParam.SearcherUserId,
            Matched = true,
            ControlCount = resultCount,
            TestCount = resultCount,
            CallId = searchParam.CallId,
            CallTime = searchParam.CallTime
        };
        
        _comparisonResults.Add(result);
        OnComparisonCompleted(new ComparisonResultEventArgs { Result = result });
        
        Log.Information("Results match for SearcherUserId: {SearcherUserId} ({Count} results)", 
            searchParam.SearcherUserId, resultCount);
    }

    protected virtual void OnProgressUpdated(ComparisonProgressEventArgs e)
    {
        ProgressUpdated?.Invoke(this, e);
    }

    protected virtual void OnComparisonCompleted(ComparisonResultEventArgs e)
    {
        ComparisonCompleted?.Invoke(this, e);
    }

    protected virtual void OnDifferenceFound(ComparisonResultEventArgs e)
    {
        DifferenceFound?.Invoke(this, e);
    }

    public void ClearResults()
    {
        _comparisonResults.Clear();
    }
}
