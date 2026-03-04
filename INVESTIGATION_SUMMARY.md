# Shard 4 Reverse Search Investigation - Summary

## Investigation Date
March 4, 2026

## Issue
Shard 4 Reverse search comparisons are showing inaccurate results - Control and Test environments are returning different user IDs when they should match.

## Root Cause Analysis

### PRIMARY SUSPECT: Missing OtherUserId Parameter

The **ReverseService** implementation is missing a critical parameter:

**What's Wrong:**
```csharp
// Current Implementation - ReverseService.cs
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,  // ✓ Who is searching
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo);
// ❌ MISSING: No parameter for which user to reverse-search for
```

**What It Should Be:**
```csharp
// Expected Implementation (similar to MoreLikeThisService)
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,
    otherUserId: searcher.OtherUserId,  // ✓ ADD THIS - which user to search for
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo);
```

### Why This Causes Inaccuracy

Reverse searches answer the question: *"Who would see User X in their search results?"*

Without the `OtherUserId` (User X), the search service:
1. May use a default or fallback user ID
2. Could pick a non-deterministic user
3. Might behave differently on Control vs Test servers
4. Results in different user lists being returned

### Why Shard 4 Specifically?

Shard 4 may exhibit this problem more prominently due to:
- **Different data distribution**: More diverse user demographics
- **Higher user activity**: More active users = more variability
- **Site-specific behavior**: Different site codes on shard 4 may have different default behaviors
- **Server configuration**: The two servers (DA1MASC804 vs DA1MASC805) may handle missing OtherUserId differently

## Actions Taken

### 1. Created Diagnostic Documentation
- **SHARD4_DIAGNOSTIC.md**: Comprehensive troubleshooting guide
- **DiagnosticQueries.sql**: SQL queries to investigate the issue

### 2. Added Enhanced Logging
Updated `ReverseService.cs` to add diagnostic logging specifically for shard 4:
- Logs when OtherUserId is available but not passed
- Logs when OtherUserId is missing from the database
- Helps identify whether the issue is in the code or data

### 3. Investigation Tools

#### SQL Diagnostic Queries
Run `DiagnosticQueries.sql` to check:
- Query 1: Verify if OtherUserId is populated in the database
- Query 2: Compare OtherUserId population across all shards
- Query 4: Identify unique characteristics of shard 4
- Query 7: Check server-specific behavior patterns

#### Code Diagnostics
The enhanced logging will now output:
```
SHARD 4 DIAGNOSTIC - Reverse Search: Server=DA1MASC805, SiteCode=1, SearcherUserId=12345, OtherUserId=67890, CallId=...
WARNING: OtherUserId=67890 is available but NOT passed to ReverseArgs. This may cause inaccurate comparisons.
```

## Next Steps

### STEP 1: Verify the Root Cause (URGENT)
Run this SQL query to check if OtherUserId is in the database:

```sql
SELECT TOP 20
    lg.SearcherUserID,
    lg.OtherUserId,  -- Is this NULL or populated?
    lg.SiteCode,
    sh.ShardID
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE sh.ShardID = 4
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
ORDER BY lg.CallTime DESC;
```

**Expected Results:**
- If OtherUserId is **populated** (has values): The fix is to update ReverseService
- If OtherUserId is **NULL**: Need to investigate why it's not being logged

### STEP 2: Check ReverseArgs Constructor (CRITICAL)
We need to verify if the `ReverseArgs` class supports an `otherUserId` parameter:

**Option A: Check MrSIXProxyV2 Documentation**
- Look for ReverseArgs constructor signature
- Verify parameter names and types

**Option B: Test in Code**
Add this diagnostic code to see what parameters are available:
```csharp
// In ReverseService.cs - temporary diagnostic
var argsType = typeof(MrSIXProxyV2.Input.ReverseArgs);
var constructors = argsType.GetConstructors();
foreach (var ctor in constructors)
{
    Log.Information("ReverseArgs Constructor: {Parameters}", 
        string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}")));
}
```

### STEP 3: Apply the Fix
Once confirmed that ReverseArgs supports otherUserId parameter:

```csharp
// Update ReverseService.cs - Line 27-37
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,
    otherUserId: searcher.OtherUserId,  // ADD THIS LINE
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo)
{
    PinnedToServername = pinnedToServerName
};
```

### STEP 4: Test the Fix
1. Run comparison on shard 4 with the fix
2. Check mismatch rate - should drop significantly
3. Verify Control and Test return same results
4. Check logs for "SHARD 4 DIAGNOSTIC" messages

### STEP 5: Validate Across All Shards
1. Run comparisons on all shards (not just 4)
2. Ensure the fix doesn't negatively impact other shards
3. Compare mismatch rates before/after

## Alternative Fixes (If Primary Fix Doesn't Resolve)

### Fix B: Increase Data Movement Threshold
If shard 4 has more data replication lag:

```csharp
// In ComparisonService.cs or add to AppConfiguration.cs
// Shard-specific configuration
if (searchParam.ShardId == 4)
{
    _config.RecentLoginThresholdMinutes = 120; // 2 hours instead of 1
}
```

### Fix C: Add Execution Delay
Reduce timing sensitivity between Control and Test execution:

```csharp
// In ComparisonService.cs - CompareSearchParameter method
var controlResult = await searchService.ExecuteSearch(searchParam, _config.MrSixControl);
await Task.Delay(100); // 100ms delay
var testResult = await searchService.ExecuteSearch(searchParam, _config.MrSixTest);
```

## Success Criteria

The issue is resolved when:
- ✅ Shard 4 mismatch rate drops to match other shards
- ✅ Control and Test consistently return same results for same inputs
- ✅ Retry runs show consistent results (RetryMatched = true)
- ✅ No increase in mismatches on other shards

## Files Modified

1. **ReverseService.cs** - Added shard 4 diagnostic logging
2. **SHARD4_DIAGNOSTIC.md** - Comprehensive diagnostic guide (NEW)
3. **DiagnosticQueries.sql** - SQL investigation queries (NEW)
4. **INVESTIGATION_SUMMARY.md** - This summary document (NEW)

## Monitoring

After applying fixes, monitor:
1. **Mismatch Rate by Shard**: Track percentage of mismatches for each shard
2. **OtherUserId Usage**: Verify log messages show OtherUserId being passed
3. **Retry Success Rate**: Monitor RetryMatched ratio
4. **Performance**: Ensure no degradation in comparison speed

## Questions for Stakeholders

1. What is the current mismatch rate for shard 4 vs other shards?
2. Has shard 4 always had this issue, or is it recent?
3. Are there known differences in shard 4's configuration?
4. What is the expected/acceptable mismatch rate?

## Contact

For questions about this investigation:
- Review: SHARD4_DIAGNOSTIC.md
- Run: DiagnosticQueries.sql
- Check: Logs for "SHARD 4 DIAGNOSTIC" messages
