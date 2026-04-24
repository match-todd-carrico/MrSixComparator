using System.Collections.Concurrent;
using System.Diagnostics;
using Serilog;
using MrSIXProxyV2.ResultsV4;
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

/// <summary>
/// A ClassName that was present in the input but did not run, grouped so the UI can
/// show what needs manual coverage vs. what was just toggled off.
/// </summary>
/// <param name="ClassName">The raw ClassName as it appeared in SearchLog (after any normalization, e.g. Sticker).</param>
/// <param name="Count">Number of cached parameters that were skipped for this ClassName.</param>
/// <param name="IsSupportedByTool">
/// True when ComparisonService has a dispatch entry for this ClassName (so it was skipped only because
/// the user toggled the service off in the UI). False when the tool has no implementation for this
/// ClassName yet — these are the cases that need manual coverage.
/// </param>
public record SkippedClassNameInfo(string ClassName, int Count, bool IsSupportedByTool);

public class ComparisonService
{
    private readonly AppConfiguration _config;
    private readonly Dictionary<string, ISearchService> _searchServices;
    private readonly ConcurrentBag<ComparisonResult> _comparisonResults;

    public event EventHandler<ComparisonProgressEventArgs>? ProgressUpdated;
    public event EventHandler<ComparisonResultEventArgs>? ComparisonCompleted;
    public event EventHandler<ComparisonResultEventArgs>? DifferenceFound;

    public IReadOnlyList<ComparisonResult> Results => _comparisonResults.ToList();

    /// <summary>
    /// ClassNames that were present in the input but did not run, with counts and a flag indicating
    /// whether the tool has an implementation for them. Populated at the start of CompareSearchResults.
    /// </summary>
    public IReadOnlyList<SkippedClassNameInfo> SkippedClassNames { get; private set; } = Array.Empty<SkippedClassNameInfo>();

    public ComparisonService(
        AppConfiguration config,
        StackSearchService stackSearchService,
        StickerSearchService stickerSearchService,
        OnePushService onePushService,
        KeywordSearchService keywordSearchService,
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

        // Map ClassName to service instances.
        //
        // Note: SearchParameterService normalizes sticker-family rows (WhatIfSearchId 64/65/68)
        // from ClassName="Stack" to ClassName="Sticker" so dispatch stays 1:1 on ClassName.
        //
        // Intentionally NOT dispatched:
        //   SearchV4.Venus.* (rolled up from SearchV4.Venus.OneWay.<saved-search-name>, plus
        //                    .Backfill, .Triangulation, .TwoWay, .SavedSearch*)
        //     - Engine returns VenusResponse<SearchResultRow>, not SearchResponse<SearchResultRow>.
        //     - A single VenusToPoco request produces multiple SearchLog rows; one row cannot be
        //       replayed as a standalone request.
        //     - MrSIXProxyV2.SearchesV5.VenusBatch is [Obsolete].
        //   BatchSearch - per-user result list, not SearchResponse<SearchResultRow>.
        _searchServices = new Dictionary<string, ISearchService>(StringComparer.OrdinalIgnoreCase)
        {
            { "Stack", stackSearchService },
            { "Sticker", stickerSearchService },
            { "SearchV4.OnePush", onePushService },
            { "SearchV4.KeywordSearch", keywordSearchService },
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

        // Capture the breakdown of skipped ClassNames so the UI can show (a) how many parameters
        // were dropped per ClassName and (b) whether the tool even has an implementation for them.
        // "Not supported" is the list that needs manual coverage.
        var skippedBreakdown = searchParameters
            .Where(sp => !_config.EnabledSearchServices.Contains(sp.ClassName))
            .GroupBy(sp => sp.ClassName ?? "(null)")
            .Select(g => new SkippedClassNameInfo(
                ClassName: g.Key,
                Count: g.Count(),
                IsSupportedByTool: _searchServices.ContainsKey(g.Key)))
            .OrderByDescending(s => s.Count)
            .ThenBy(s => s.ClassName)
            .ToList();
        SkippedClassNames = skippedBreakdown;

        if (skippedCount > 0)
        {
            var unsupportedCount = skippedBreakdown.Where(s => !s.IsSupportedByTool).Sum(s => s.Count);
            Log.Information(
                "Skipping {SkippedCount} search parameters ({UnsupportedCount} have no tool implementation). ClassNames: {ClassNames}",
                skippedCount, unsupportedCount,
                string.Join(", ", skippedBreakdown.Select(s => $"{s.ClassName}={s.Count}{(s.IsSupportedByTool ? "" : "*")}")));
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
        var retryControlStopwatch = Stopwatch.StartNew();
        var resultControl = await searchService.ExecuteSearch(searchParam, _config.MrSixControl, enableExplain: true);
        retryControlStopwatch.Stop();

        var retryTestStopwatch = Stopwatch.StartNew();
        var resultTest = await searchService.ExecuteSearch(searchParam, _config.MrSixTest, enableExplain: true);
        retryTestStopwatch.Stop();

        Log.Information(
            "Retry timing Service={ClassName} SiteCode={SiteCode} SearcherUserId={SearcherUserId} ControlMs={ControlMs} TestMs={TestMs}",
            searchParam.ClassName, searchParam.SiteCode, searchParam.SearcherUserId,
            retryControlStopwatch.ElapsedMilliseconds, retryTestStopwatch.ElapsedMilliseconds);

        originalResult.RetryControlDurationMs = retryControlStopwatch.ElapsedMilliseconds;
        originalResult.RetryTestDurationMs = retryTestStopwatch.ElapsedMilliseconds;

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

        // Refresh StackConfig from the retry responses so the UI shows the latest values.
        originalResult.ControlStackConfig = resultControl?.SearchBag?.GetValueOrDefault("StackConfig") ?? originalResult.ControlStackConfig;
        originalResult.TestStackConfig = resultTest?.SearchBag?.GetValueOrDefault("StackConfig") ?? originalResult.TestStackConfig;

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
        
        // 1. Execute Search on both environments. Timed so the UI and logs can show where wall-clock
        // time actually went. Today these run sequentially; if/when we parallelize them the stopwatches
        // should still record each leg's latency independently.
        var controlStopwatch = Stopwatch.StartNew();
        var controlResult = await searchService.ExecuteSearch(searchParam, _config.MrSixControl);
        controlStopwatch.Stop();

        var testStopwatch = Stopwatch.StartNew();
        var testResult = await searchService.ExecuteSearch(searchParam, _config.MrSixTest);
        testStopwatch.Stop();

        long controlMs = controlStopwatch.ElapsedMilliseconds;
        long testMs = testStopwatch.ElapsedMilliseconds;

        Log.Information(
            "Comparison timing Service={ClassName} SiteCode={SiteCode} SearcherUserId={SearcherUserId} ControlMs={ControlMs} TestMs={TestMs}",
            searchParam.ClassName, searchParam.SiteCode, searchParam.SearcherUserId, controlMs, testMs);

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
        
        // 5. Filter out users with recent LastLoginDate (data movement due to eventual consistency)
        var ignoredFromControl = new List<int>();
        var ignoredFromTest = new List<int>();
        
        if (_config.IgnoreRecentLogins && (onlyInA.Any() || onlyInB.Any()))
        {
            var threshold = DateTime.UtcNow.AddMinutes(-_config.RecentLoginThresholdMinutes);
            
            ignoredFromControl = FilterRecentLogins(onlyInA, controlResult, threshold);
            ignoredFromTest = FilterRecentLogins(onlyInB, testResult, threshold);
            
            if (ignoredFromControl.Any())
            {
                onlyInA = onlyInA.Except(ignoredFromControl).ToList();
                Log.Information("Ignored {Count} users from Control with recent LastLoginDate (data movement): {UserIds}",
                    ignoredFromControl.Count, string.Join(",", ignoredFromControl));
            }
            
            if (ignoredFromTest.Any())
            {
                onlyInB = onlyInB.Except(ignoredFromTest).ToList();
                Log.Information("Ignored {Count} users from Test with recent LastLoginDate (data movement): {UserIds}",
                    ignoredFromTest.Count, string.Join(",", ignoredFromTest));
            }
        }
        
        // 6. Compare properties for users present in both result sets
        var propertyDifferences = CompareProperties(controlResult, testResult, inBoth);
        
        bool hasDifferences = onlyInA.Any() || onlyInB.Any();

        // Pull StackConfig from each server's response for visibility on Stack-search mismatches.
        // Control may be null until the feature is deployed there.
        var controlStackConfig = controlResult?.SearchBag?.GetValueOrDefault("StackConfig");
        var testStackConfig = testResult?.SearchBag?.GetValueOrDefault("StackConfig");

        if (hasDifferences)
        {
            RecordDifference(searchParam, userIdsA, userIdsB, onlyInA, onlyInB, inBoth, 
                controlResult.CallId, testResult.CallId, userIdsBySlotTypeA, userIdsBySlotTypeB,
                ignoredFromControl, ignoredFromTest, propertyDifferences,
                controlStackConfig, testStackConfig,
                controlMs, testMs);
        }
        else
        {
            RecordMatch(searchParam, userIdsA.Count, controlResult.CallId, testResult.CallId, userIdsBySlotTypeA,
                ignoredFromControl, ignoredFromTest, propertyDifferences,
                controlStackConfig, testStackConfig,
                controlMs, testMs);
        }
    }
    
    private static List<PropertyDifference> CompareProperties(
        SearchResponse<SearchResultRow> controlResponse,
        SearchResponse<SearchResultRow> testResponse,
        List<int> inBoth)
    {
        var differences = new List<PropertyDifference>();
        
        if (!inBoth.Any() || controlResponse?.Results == null || testResponse?.Results == null)
            return differences;
        
        var controlByUserId = new Dictionary<int, (int Position, SearchResultRow Row)>();
        for (int i = 0; i < controlResponse.Results.Count; i++)
        {
            var row = controlResponse.Results[i];
            controlByUserId.TryAdd(row.UserId, (i + 1, row));
        }
        
        var testByUserId = new Dictionary<int, (int Position, SearchResultRow Row)>();
        for (int i = 0; i < testResponse.Results.Count; i++)
        {
            var row = testResponse.Results[i];
            testByUserId.TryAdd(row.UserId, (i + 1, row));
        }
        
        foreach (var userId in inBoth)
        {
            if (!controlByUserId.TryGetValue(userId, out var ctrl) ||
                !testByUserId.TryGetValue(userId, out var test))
                continue;
            
            void Cmp(string name, string controlVal, string testVal)
            {
                if (!string.Equals(controlVal, testVal, StringComparison.Ordinal))
                    differences.Add(new PropertyDifference
                    {
                        UserId = userId,
                        PropertyName = name,
                        ControlValue = controlVal,
                        TestValue = testVal
                    });
            }
            
            Cmp("Position", ctrl.Position.ToString(), test.Position.ToString());
            Cmp("Relevance", ctrl.Row.MatchCnt.ToString("G"), test.Row.MatchCnt.ToString("G"));
            Cmp("AlgoId", ctrl.Row.ResultSlotType.ToString(), test.Row.ResultSlotType.ToString());
            Cmp("Rank", ctrl.Row.Rank.ToString(), test.Row.Rank.ToString());
            Cmp("ReverseRank", ctrl.Row.ReverseRank.ToString(), test.Row.ReverseRank.ToString());
            Cmp("AbsoluteMatch", ctrl.Row.AbsoluteMatch.ToString(), test.Row.AbsoluteMatch.ToString());
            Cmp("FirstTie", ctrl.Row.FirstTie.ToString(), test.Row.FirstTie.ToString());
            Cmp("SecondTie", ctrl.Row.SecondTie.ToString(), test.Row.SecondTie.ToString());
            Cmp("ThirdTie", ctrl.Row.ThirdTie.ToString(), test.Row.ThirdTie.ToString());
            Cmp("FourthTie", ctrl.Row.FourthTie.ToString(), test.Row.FourthTie.ToString());
            Cmp("FifthTie", ctrl.Row.FifthTie.ToString(), test.Row.FifthTie.ToString());
            Cmp("SixthTie", ctrl.Row.SixthTie.ToString(), test.Row.SixthTie.ToString());
            Cmp("Distance", ctrl.Row.Distance.ToString("F2"), test.Row.Distance.ToString("F2"));
            Cmp("Handle", ctrl.Row.Handle ?? "", test.Row.Handle ?? "");
            Cmp("ConnectionToMatch", ctrl.Row.ConnectionToMatch.ToString(), test.Row.ConnectionToMatch.ToString());
            Cmp("ConnectionToSearcher", ctrl.Row.ConnectionToSearcher.ToString(), test.Row.ConnectionToSearcher.ToString());
        }
        
        if (differences.Any())
        {
            Log.Information("Found {Count} property differences across {UserCount} users in both result sets",
                differences.Count, differences.Select(d => d.UserId).Distinct().Count());
        }
        
        return differences;
    }

    /// <summary>
    /// Filters out user IDs from the "only in" list whose LastLoginDate is within the threshold.
    /// These are likely data movement artifacts from the eventually consistent data model.
    /// </summary>
    private static List<int> FilterRecentLogins(
        List<int> onlyInUserIds, 
        SearchResponse<SearchResultRow> response, 
        DateTime threshold)
    {
        if (response?.Results == null || !onlyInUserIds.Any())
            return new List<int>();
        
        var recentUserIds = new List<int>();
        var onlyInSet = new HashSet<int>(onlyInUserIds);
        
        foreach (var row in response.Results)
        {
            if (onlyInSet.Contains(row.UserId) && row.LastLoginDate >= threshold)
            {
                recentUserIds.Add(row.UserId);
            }
        }
        
        return recentUserIds;
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
        Dictionary<string, List<int>> slotTypeB,
        List<int>? ignoredFromControl = null,
        List<int>? ignoredFromTest = null,
        List<PropertyDifference>? propertyDifferences = null,
        string? controlStackConfig = null,
        string? testStackConfig = null,
        long controlDurationMs = 0,
        long testDurationMs = 0)
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
            InBothBySlotType = inBothBySlotType,
            IgnoredFromControl = ignoredFromControl ?? new List<int>(),
            IgnoredFromTest = ignoredFromTest ?? new List<int>(),
            PropertyDifferences = propertyDifferences ?? new List<PropertyDifference>(),
            SourceStackConfig = searchParam.SourceStackConfig,
            ControlStackConfig = controlStackConfig,
            TestStackConfig = testStackConfig,
            ControlDurationMs = controlDurationMs,
            TestDurationMs = testDurationMs
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
        if (searchParam.SourceStackConfig != null || controlStackConfig != null || testStackConfig != null)
        {
            Log.Warning("StackConfig - Source: {SourceStackConfig}, Control: {ControlStackConfig}, Test: {TestStackConfig}",
                searchParam.SourceStackConfig ?? "<none>",
                controlStackConfig ?? "<none>",
                testStackConfig ?? "<none>");
        }
        if (ignoredFromControl?.Any() == true)
            Log.Information("Ignored from Control (data movement): {Ignored}", string.Join(",", ignoredFromControl));
        if (ignoredFromTest?.Any() == true)
            Log.Information("Ignored from Test (data movement): {Ignored}", string.Join(",", ignoredFromTest));
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

    private void RecordMatch(SearchParameter searchParam, int resultCount, Guid controlCallId, Guid testCallId, Dictionary<string, List<int>> slotTypeMapping,
        List<int>? ignoredFromControl = null, List<int>? ignoredFromTest = null,
        List<PropertyDifference>? propertyDifferences = null,
        string? controlStackConfig = null, string? testStackConfig = null,
        long controlDurationMs = 0, long testDurationMs = 0)
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
            InBothBySlotType = slotTypeMapping, // All results matched, so store in InBoth
            IgnoredFromControl = ignoredFromControl ?? new List<int>(),
            IgnoredFromTest = ignoredFromTest ?? new List<int>(),
            PropertyDifferences = propertyDifferences ?? new List<PropertyDifference>(),
            SourceStackConfig = searchParam.SourceStackConfig,
            ControlStackConfig = controlStackConfig,
            TestStackConfig = testStackConfig,
            ControlDurationMs = controlDurationMs,
            TestDurationMs = testDurationMs
        };
        
        _comparisonResults.Add(result);
        OnComparisonCompleted(new ComparisonResultEventArgs { Result = result });
        
        var ignoredTotal = (ignoredFromControl?.Count ?? 0) + (ignoredFromTest?.Count ?? 0);
        if (ignoredTotal > 0)
        {
            Log.Information("Results match for SearcherUserId: {SearcherUserId} ({Count} results, {Ignored} ignored as data movement)", 
                searchParam.SearcherUserId, resultCount, ignoredTotal);
        }
        else
        {
            Log.Information("Results match for SearcherUserId: {SearcherUserId} ({Count} results)", 
                searchParam.SearcherUserId, resultCount);
        }
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
