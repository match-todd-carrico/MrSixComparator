# Shard 4 Reverse Search Diagnostic Guide

## Problem
Shard 4 Reverse search comparisons are showing inaccurate results compared to other shards.

## Potential Root Causes

### 1. **Missing OtherUserId Parameter** (CRITICAL)
**Issue**: The `ReverseService` does NOT pass the `OtherUserId` parameter to `ReverseArgs`.

**Evidence**:
```csharp
// ReverseService.cs - Current Implementation (MISSING OtherUserId)
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,  // Who is searching
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo)
{
    PinnedToServername = pinnedToServerName
};
// PROBLEM: No otherUserId parameter - which user to search for!
```

**Comparison** with MoreLikeThisService (similar search type):
```csharp
// MoreLikeThisService.cs - CORRECT Implementation
var args = new MoreLikeThisArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,
    moreLikeThisUserId: searcher.OtherUserId,  // ✓ Properly passes OtherUserId
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo)
```

**Why This Causes Issues**:
- Reverse searches typically need to know WHICH user profile to reverse-search against
- Without `OtherUserId`, the search service may:
  - Use a default/fallback user ID
  - Pick a random or non-deterministic user
  - Use different logic on Control vs Test servers
- This would cause different results between Control and Test environments

**Why Shard 4 Specifically**:
- Different data distribution or user demographics on shard 4
- Different default behavior when OtherUserId is missing
- Server configuration differences specific to shard 4

### 2. **Data Movement / Eventual Consistency**
**Issue**: Shard 4 may have more active users, causing more data replication lag.

**Current Mitigation**:
```csharp
// AppConfiguration.cs
public bool IgnoreRecentLogins { get; set; } = true;
public int RecentLoginThresholdMinutes { get; set; } = 60;  // 1 hour
```

**Why This May Be Insufficient for Shard 4**:
- Higher user activity on shard 4 = more frequent data changes
- 60-minute threshold may be too short for shard 4's replication lag
- Different data centers or replication topology for shard 4

### 3. **Sequential Search Execution Timing**
**Issue**: Control and Test searches run sequentially, not simultaneously.

**Code Flow**:
```csharp
// ComparisonService.cs - Line 275-276
var controlResult = await searchService.ExecuteSearch(searchParam, _config.MrSixControl);
var testResult = await searchService.ExecuteSearch(searchParam, _config.MrSixTest);
// Time gap between these two calls = opportunity for data changes
```

**Why This Affects Shard 4**:
- If shard 4 has higher write activity
- Data could change between control and test execution
- More pronounced with active Reverse search users

### 4. **Shard-Specific Server Configuration**
**Issue**: DA1MASC805 (Control) and DA1MASC804 (Test) may have different configurations for shard 4.

**Potential Differences**:
- Search index refresh rates
- Cache settings
- Algorithm versions
- Feature flags
- Data center locations

## Diagnostic Steps

### Step 1: Check Engine Status for Shard 4
Run this query to verify both servers report the same shard:

```csharp
var controlStatus = MrSixContextService.GetEngineStatus("DA1MASC805");
var testStatus = MrSixContextService.GetEngineStatus("DA1MASC804");

// Check StatusBag["ShardId"]
```

Expected: Both should return ShardId = 4

### Step 2: Inspect OtherUserId Values
Query the database to check if Reverse searches have valid OtherUserId:

```sql
SELECT TOP 100
    lg.CallID,
    lg.SearcherUserID,
    lg.OtherUserId,  -- Check if this is populated
    lg.ClassName,
    lg.SiteCode,
    sh.ShardID
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE sh.ShardID = 4
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
ORDER BY lg.CallTime DESC
```

**Check**:
- Are OtherUserId values populated (not NULL)?
- Do they look valid (> 999)?
- Are they consistent?

### Step 3: Compare ReverseArgs Parameters
Add diagnostic logging to ReverseService to capture what's being sent:

```csharp
// In ReverseService.cs - Before Execute
Log.Information("Reverse Search Args - Server: {Server}, SiteCode: {SiteCode}, ShardId: {ShardId}, SearcherUserId: {SearcherUserId}, OtherUserId: {OtherUserId}", 
    pinnedToServerName, 
    searcher.SiteCode, 
    searcher.ShardId, 
    searcher.SearcherUserId,
    searcher.OtherUserId);  // Log this even though it's not being passed
```

### Step 4: Check Site Code Distribution
Verify which site codes are on shard 4:

```sql
SELECT 
    sh.SiteCode,
    sh.ShardID,
    COUNT(DISTINCT lg.SearcherUserID) as UniqueSearchers,
    COUNT(*) as TotalReverseSearches
FROM Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
LEFT JOIN SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
    ON lg.SiteCode = sh.SiteCode
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
WHERE sh.ShardID = 4
GROUP BY sh.SiteCode, sh.ShardID
ORDER BY TotalReverseSearches DESC
```

### Step 5: Compare Specific Mismatched Results
For a known mismatch on shard 4:
1. Get the ControlCallId and TestCallId
2. Visit the explain endpoints:
   - Control: `http://DA1MASC805:8888/admin/getExplain?callid={ControlCallId}`
   - Test: `http://DA1MASC804:8888/admin/getExplain?callid={TestCallId}`
3. Compare:
   - Query parameters sent
   - OtherUserId (if present)
   - Scoring differences
   - Filter criteria

### Step 6: Test with Fixed OtherUserId
Modify ReverseService temporarily to check if adding OtherUserId fixes the issue:

```csharp
// Temporary diagnostic code
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo);

// TRY: Check if ReverseArgs has an OtherUserId property we can set
// args.OtherUserId = searcher.OtherUserId;  // Add this if property exists
```

## Recommended Fixes

### Fix #1: Add OtherUserId to ReverseArgs (PRIMARY FIX)
```csharp
// Update ReverseService.cs to pass OtherUserId
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,
    otherUserId: searcher.OtherUserId,  // ADD THIS PARAMETER
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo)
{
    PinnedToServername = pinnedToServerName
};
```

**Note**: This assumes `ReverseArgs` constructor supports an `otherUserId` parameter. Check the MrSIXProxyV2 library documentation.

### Fix #2: Increase Recent Login Threshold for Shard 4
```csharp
// In AppConfiguration.cs or add shard-specific config
public int RecentLoginThresholdMinutes { get; set; } = 120;  // Increase from 60 to 120 for shard 4
```

Or add shard-specific configuration:
```csharp
public Dictionary<int, int> ShardSpecificRecentLoginThresholds { get; set; } = new()
{
    { 4, 120 }  // 2 hours for shard 4
};
```

### Fix #3: Add Retry Logic with Delay
```csharp
// In ComparisonService.cs
// Add a small delay between control and test execution to reduce timing sensitivity
var controlResult = await searchService.ExecuteSearch(searchParam, _config.MrSixControl);
await Task.Delay(100);  // 100ms delay
var testResult = await searchService.ExecuteSearch(searchParam, _config.MrSixTest);
```

### Fix #4: Enhanced Logging for Shard 4
```csharp
// Add conditional logging in ReverseService.cs
if (searcher.ShardId == 4)
{
    Log.Warning("Shard 4 Reverse Search - Extra Diagnostics - SearcherUserId: {SearcherUserId}, OtherUserId: {OtherUserId}, SiteCode: {SiteCode}, CallId: {CallId}",
        searcher.SearcherUserId,
        searcher.OtherUserId,
        searcher.SiteCode,
        searcher.CallId);
}
```

## Testing Plan

1. **Baseline Test**: Run comparisons on shard 4 with current code, document mismatch rate
2. **Fix #1 Test**: Apply OtherUserId fix, re-run comparisons
3. **Fix #2 Test**: If #1 doesn't fully resolve, increase threshold
4. **Comparison**: Compare mismatch rates across shards
5. **Validation**: Verify fixes don't negatively impact other shards

## Expected Outcome

If OtherUserId is the root cause:
- Mismatch rate on shard 4 should drop significantly (ideally to match other shards)
- Results should be consistent between control and test
- Retries should show same results as initial run

## Next Steps

1. Run Step 2 (Check OtherUserId values in database)
2. If OtherUserId is populated, proceed with Fix #1
3. If OtherUserId is NULL, investigate why Reverse searches aren't logging it
4. Monitor mismatch rate after fixes

## Questions to Answer

- [ ] Does `ReverseArgs` constructor accept an `otherUserId` parameter?
- [ ] Are OtherUserId values populated in SearchLog for Reverse searches?
- [ ] What is the current mismatch rate for shard 4 vs other shards?
- [ ] Do Control and Test servers have the same ShardId configuration?
- [ ] Are there any known differences in shard 4's data characteristics?
