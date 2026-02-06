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
    private readonly Dictionary<string, ISearchService> _searchServices;
    private readonly ConcurrentBag<ComparisonResult> _comparisonResults;

    public event EventHandler<ComparisonProgressEventArgs>? ProgressUpdated;
    public event EventHandler<ComparisonResultEventArgs>? ComparisonCompleted;
    public event EventHandler<ComparisonResultEventArgs>? DifferenceFound;

    public IReadOnlyList<ComparisonResult> Results => _comparisonResults.ToList();

    public ComparisonService(
        AppConfiguration config,
        StackSearchService stackSearchService,
        OnePushService onePushService,
        LitBatchService litBatchService,
        LitSearchService litSearchService,
        MoreLikeThisService moreLikeThisService,
        OneWayService oneWayService,
        ExpertPicksService expertPicksService,
        JustForYouService justForYouService,
        MatchPicksService matchPicksService,
        ReverseService reverseService,
        SearchWowService searchWowService,
        TwoWayService twoWayService)
    {
        _config = config;
        _comparisonResults = new ConcurrentBag<ComparisonResult>();
        
        // Map ClassName to service instances
        _searchServices = new Dictionary<string, ISearchService>(StringComparer.OrdinalIgnoreCase)
        {
            { "Stack", stackSearchService },
            { "SearchV4.OnePush", onePushService },
            { "SearchHighlight.LitBatch", litBatchService },
            { "SearchHighlight.LitSearch", litSearchService },
            { "SearchV4.MoreLikeThis", moreLikeThisService },
            { "SearchV4.OneWay", oneWayService },
            { "SearchV4.Recommended.ExpertPicks", expertPicksService },
            { "SearchV4.Recommended.JustForYou", justForYouService },
            { "SearchV4.Recommended.MatchPicks", matchPicksService },
            { "SearchV4.Reverse", reverseService },
            { "SearchV4.SearchWow", searchWowService },
            { "SearchV4.TwoWay", twoWayService }
        };
    }

    public async Task CompareSearchResults(List<SearchParameter> searchParameters, CancellationToken cancellationToken = default)
    {
        // Filter search parameters based on enabled services
        var enabledSearchParameters = searchParameters
            .Where(sp => _config.EnabledSearchServices.Contains(sp.ClassName))
            .ToList();
        
        int skippedCount = searchParameters.Count - enabledSearchParameters.Count;
        if (skippedCount > 0)
        {
            Log.Information("Skipping {SkippedCount} search parameters with disabled service types", skippedCount);
        }

        int total = enabledSearchParameters.Count;
        int current = 0;

        OnProgressUpdated(new ComparisonProgressEventArgs 
        { 
            Current = 0, 
            Total = total, 
            Message = $"Starting comparison of {total} searches (skipped {skippedCount} disabled services)..." 
        });

        await Parallel.ForEachAsync(
            enabledSearchParameters, 
            new ParallelOptions 
            { 
                MaxDegreeOfParallelism = _config.MaxParallelism,
                CancellationToken = cancellationToken 
            }, 
            async (searchParam, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    await CompareSearchParameter(searchParam);
                }
                catch (OperationCanceledException)
                {
                    throw; // Let cancellation propagate
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

        // Retry mismatched comparisons if enabled
        if (_config.AutoRetryMismatches)
        {
            await RetryMismatchedComparisons(enabledSearchParameters, cancellationToken);
        }

        OnProgressUpdated(new ComparisonProgressEventArgs 
        { 
            Current = total, 
            Total = total, 
            Message = "Comparison complete!" 
        });
    }

    private async Task RetryMismatchedComparisons(List<SearchParameter> originalSearchParameters, CancellationToken cancellationToken = default)
    {
        // Get all mismatched results
        var mismatchedResults = _comparisonResults.Where(r => !r.Matched).ToList();
        
        if (!mismatchedResults.Any())
        {
            Log.Information("No mismatches to retry");
            return;
        }

        int retryCount = mismatchedResults.Count;
        int current = 0;

        Log.Information("Retrying {RetryCount} mismatched comparisons", retryCount);
        OnProgressUpdated(new ComparisonProgressEventArgs 
        { 
            Current = 0, 
            Total = retryCount, 
            Message = $"Retrying {retryCount} mismatched comparisons to verify repeatability..." 
        });

        await Parallel.ForEachAsync(
            mismatchedResults,
            new ParallelOptions 
            { 
                MaxDegreeOfParallelism = _config.MaxParallelism,
                CancellationToken = cancellationToken 
            },
            async (mismatchedResult, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    // Find the original search parameter
                    var searchParam = originalSearchParameters.FirstOrDefault(sp => 
                        sp.SearcherUserId == mismatchedResult.SearcherUserId && 
                        sp.SiteCode == mismatchedResult.SiteCode &&
                        sp.ClassName == mismatchedResult.SearchServiceName);

                    if (searchParam != null)
                    {
                        await RetryComparisonForResult(mismatchedResult, searchParam);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw; // Let cancellation propagate
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to retry search. SiteCode: {SiteCode}, SearcherUserId: {SearcherUserId}",
                        mismatchedResult.SiteCode, mismatchedResult.SearcherUserId);
                }
                finally
                {
                    current++;
                    OnProgressUpdated(new ComparisonProgressEventArgs 
                    { 
                        Current = current, 
                        Total = retryCount, 
                        Message = $"Retried {current} of {retryCount} mismatches..." 
                    });
                }
            });

        // Log retry summary
        var confirmedMismatches = mismatchedResults.Count(r => r.RetryMatched == false);
        var nowMatching = mismatchedResults.Count(r => r.RetryMatched == true);
        Log.Information("Retry complete. Confirmed mismatches: {Confirmed}, Now matching: {NowMatching}", 
            confirmedMismatches, nowMatching);
    }

    private async Task RetryComparisonForResult(ComparisonResult originalResult, SearchParameter searchParam)
    {
        Log.Information("Retrying comparison for SearcherUserId: {SearcherUserId}, Service: {Service}", 
            searchParam.SearcherUserId, searchParam.ClassName);
        
        // Get the appropriate search service
        if (!_searchServices.TryGetValue(searchParam.ClassName, out var searchService))
        {
            Log.Error("No search service found for ClassName: {ClassName}", searchParam.ClassName);
            return;
        }
        
        Log.Debug("Generated Retry CallIds - Control: {ControlCallId}, Test: {TestCallId}", originalResult.ControlCallId, originalResult.CallId);
        
        // Execute Search on both environments again WITH EXPLAIN ENABLED
        var resultControl = await searchService.ExecuteSearch(searchParam, _config.MrSixControl, enableExplain: true);
        var resultTest = await searchService.ExecuteSearch(searchParam, _config.MrSixTest, enableExplain: true);

        // Extract UserIds from results
        var userIdsControl = searchService.ExtractUserIds(resultControl);
        var userIdsTest = searchService.ExtractUserIds(resultTest);

        // Update the original result with retry information
        originalResult.WasRetried = true;
        originalResult.RetryControlCount = userIdsControl.Count;
        originalResult.RetryTestCount = userIdsTest.Count;
        originalResult.RetryMatched = userIdsControl.Count == userIdsTest.Count && 
                                       !userIdsControl.Except(userIdsTest).Any() && 
                                       !userIdsTest.Except(userIdsControl).Any();

        // If retry still mismatched, update the CallIds to the retry ones for debugging
        if (originalResult.RetryMatched == false)
        {
            originalResult.ControlCallId = resultControl.CallId;
            originalResult.TestCallId = resultTest.CallId;
            Log.Information("Retry confirmed mismatch for SearcherUserId: {SearcherUserId} - Updated CallIds with EXPLAIN enabled: Control={ControlCallId}, Test={TestCallId}", 
                searchParam.SearcherUserId, resultControl.CallId, resultTest.CallId);
        }
        else
        {
            Log.Warning("Retry matched for SearcherUserId: {SearcherUserId} - original mismatch may have been transient", 
                searchParam.SearcherUserId);
        }
        
        // Fire event to update UI
        OnComparisonCompleted(new ComparisonResultEventArgs { Result = originalResult });
    }


    private async Task CompareSearchParameter(SearchParameter searchParam)
    {
        Log.Information("Starting comparison for {Description} using {ClassName}", searchParam.Description, searchParam.ClassName);
        
        // Get the appropriate search service based on ClassName
        if (!_searchServices.TryGetValue(searchParam.ClassName, out var searchService))
        {
            Log.Error("No search service found for ClassName: {ClassName}", searchParam.ClassName);
            throw new InvalidOperationException($"No search service registered for ClassName: {searchParam.ClassName}");
        }
        
        // 1. Execute Search on both environments
        var controlResult = await searchService.ExecuteSearch(searchParam, _config.MrSixControl);
        var testResult = await searchService.ExecuteSearch(searchParam, _config.MrSixTest);

        // 2. Extract UserIds from results
        var userIdsA = searchService.ExtractUserIds(controlResult);
        var userIdsB = searchService.ExtractUserIds(testResult);

        // 3. Extract UserIds by SlotType
        var userIdsBySlotTypeA = searchService.ExtractUserIdsBySlotType(controlResult);
        var userIdsBySlotTypeB = searchService.ExtractUserIdsBySlotType(testResult);

        // 4. Compare UserIds
        var onlyInA = userIdsA.Except(userIdsB).ToList();
        var onlyInB = userIdsB.Except(userIdsA).ToList();
        var inBoth = userIdsA.Intersect(userIdsB).ToList();
        
        bool hasDifferences = onlyInA.Any() || onlyInB.Any() || userIdsA.Count != userIdsB.Count;

        if (hasDifferences)
        {
            RecordDifference(searchParam, userIdsA, userIdsB, onlyInA, onlyInB, inBoth, 
                controlResult.CallId, testResult.CallId, userIdsBySlotTypeA, userIdsBySlotTypeB);
        }
        else
        {
            RecordMatch(searchParam, userIdsA.Count, controlResult.CallId, testResult.CallId, userIdsBySlotTypeA);
        }
    }

    private void RecordDifference(
        SearchParameter searchParam, 
        List<int> userIdsA, 
        List<int> userIdsB,
        List<int> onlyInA,
        List<int> onlyInB,
        List<int> inBoth,
        Guid controlCallId,
        Guid testCallId,
        Dictionary<string, List<int>> slotTypeA,
        Dictionary<string, List<int>> slotTypeB)
    {
        // Calculate slot type breakdown
        var onlyInControlBySlotType = CalculateSlotTypeBreakdown(onlyInA, slotTypeA);
        var onlyInTestBySlotType = CalculateSlotTypeBreakdown(onlyInB, slotTypeB);
        var inBothBySlotType = CalculateSlotTypeBreakdown(inBoth, slotTypeA); // Use slotTypeA for inBoth since they exist in both
        
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
            CallId = searchParam.CallId, // Original CallId from database
            ControlCallId = controlCallId, // Actual CallId used for Control search
            TestCallId = testCallId, // Actual CallId used for Test search
            CallTime = searchParam.CallTime,
            SearchServiceName = searchParam.ClassName,
            OnlyInControlBySlotType = onlyInControlBySlotType,
            OnlyInTestBySlotType = onlyInTestBySlotType,
            InBothBySlotType = inBothBySlotType
        };
        
        _comparisonResults.Add(result);
        OnDifferenceFound(new ComparisonResultEventArgs { Result = result });
        OnComparisonCompleted(new ComparisonResultEventArgs { Result = result });
        
        // Log the difference
        Log.Warning("Difference found for SearcherUserId: {SearcherUserId}, SiteCode: {SiteCode}, ControlCallId: {ControlCallId}, TestCallId: {TestCallId}",
            searchParam.SearcherUserId, searchParam.SiteCode, controlCallId, testCallId);
        Log.Warning("Control count: {ControlCount}, Test count: {TestCount}", userIdsA.Count, userIdsB.Count);
        Log.Warning("Only in Control: {OnlyInControl}", string.Join(",", onlyInA));
        Log.Warning("Only in Test: {OnlyInTest}", string.Join(",", onlyInB));
    }

    private Dictionary<string, List<int>> CalculateSlotTypeBreakdown(List<int> userIds, Dictionary<string, List<int>> slotTypeMapping)
    {
        var result = new Dictionary<string, List<int>>();
        
        foreach (var userId in userIds)
        {
            foreach (var kvp in slotTypeMapping)
            {
                if (kvp.Value.Contains(userId))
                {
                    if (!result.ContainsKey(kvp.Key))
                        result[kvp.Key] = new List<int>();
                    
                    result[kvp.Key].Add(userId);
                    break; // Each userId should only belong to one slot type
                }
            }
        }
        
        return result;
    }

    private void RecordMatch(SearchParameter searchParam, int resultCount, Guid controlCallId, Guid testCallId, Dictionary<string, List<int>> slotTypeMapping)
    {
        // Track this match
        var result = new ComparisonResult
        {
            SiteCode = searchParam.SiteCode,
            SearcherUserId = searchParam.SearcherUserId,
            Matched = true,
            ControlCount = resultCount,
            TestCount = resultCount,
            CallId = searchParam.CallId, // Original CallId from database
            ControlCallId = controlCallId, // Actual CallId used for Control search
            TestCallId = testCallId, // Actual CallId used for Test search
            CallTime = searchParam.CallTime,
            SearchServiceName = searchParam.ClassName,
            InBothBySlotType = slotTypeMapping // All results matched, so store in InBoth
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

    public IReadOnlyList<string> GetAvailableSearchServices()
    {
        return _searchServices.Keys.OrderBy(k => k).ToList();
    }
}
