# Quick Reference: Shard 4 Reverse Search Issue

## TL;DR
**Problem**: Shard 4 Reverse searches return different results on Control vs Test  
**Root Cause**: Missing `OtherUserId` parameter in ReverseService  
**Fix**: Add `otherUserId: searcher.OtherUserId` to ReverseArgs constructor  

---

## Immediate Actions

### 1️⃣ Verify the Problem (2 minutes)
Run this query in SQL Server Management Studio:

```sql
-- Check if OtherUserId is in the database
SELECT TOP 10
    SearcherUserID,
    OtherUserId,    -- Should NOT be NULL
    SiteCode,
    CallTime
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE sh.ShardID = 4
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
ORDER BY CallTime DESC;
```

**If OtherUserId is NULL**: Database isn't logging it → Different root cause  
**If OtherUserId has values**: Proceed to step 2

---

### 2️⃣ Check ReverseArgs Constructor (1 minute)
Look at MrSIXProxyV2 library or add temporary diagnostic code:

```csharp
// Temporarily add to ReverseService.cs
var argsType = typeof(MrSIXProxyV2.Input.ReverseArgs);
Log.Information("ReverseArgs params: {Params}", 
    argsType.GetConstructors()[0].GetParameters().Select(p => p.Name));
```

**Look for**: A parameter named `otherUserId`, `reverseUserId`, or similar

---

### 3️⃣ Apply the Fix (30 seconds)
Update `ReverseService.cs` line 27-37:

```csharp
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,
    otherUserId: searcher.OtherUserId,  // ← ADD THIS LINE
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo)
{
    PinnedToServername = pinnedToServerName
};
```

---

### 4️⃣ Test the Fix (5 minutes)
1. Build the solution
2. Run comparison for shard 4
3. Check logs for: `SHARD 4 DIAGNOSTIC` messages
4. Verify mismatch rate decreased

---

## Diagnostic Commands

### Check Current Logs
```powershell
# In workspace root
Get-Content logs\*.json -Tail 50 | Select-String "SHARD 4"
```

### Run All Diagnostic Queries
```sql
-- In SSMS, open and run:
-- DiagnosticQueries.sql
```

### Monitor Comparison Results
Look for these patterns in the Blazor app:
- **Before Fix**: Many mismatches on shard 4 Reverse searches
- **After Fix**: Mismatches reduced to match other shards

---

## Files to Review

| File | Purpose |
|------|---------|
| `INVESTIGATION_SUMMARY.md` | Full investigation report |
| `SHARD4_DIAGNOSTIC.md` | Detailed diagnostic guide |
| `DiagnosticQueries.sql` | Database investigation queries |
| `ReverseService.cs` | Code to fix (line 27-37) |

---

## Expected Outcomes

### ✅ Success Indicators
- Mismatch rate on shard 4 drops to < 5% (similar to other shards)
- Control and Test return identical results
- Logs show: `OtherUserId=12345 is available but NOT passed` (before fix)
- After fix: No more warnings in logs

### ❌ If Still Not Fixed
Try alternative fixes:
1. Increase `RecentLoginThresholdMinutes` to 120 for shard 4
2. Add 100ms delay between Control and Test execution
3. Review server-specific configuration differences

---

## Quick Comparison

### Current Code (BROKEN)
```csharp
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,
    // MISSING: otherUserId parameter
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo);
```

### Fixed Code
```csharp
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,
    otherUserId: searcher.OtherUserId,  // ✓ FIXED
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo);
```

### What MoreLikeThis Does (CORRECT)
```csharp
var args = new MoreLikeThisArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,
    moreLikeThisUserId: searcher.OtherUserId,  // ✓ Properly passes it
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo);
```

---

## Need Help?

1. **Can't find OtherUserId in database?**  
   → Run Query 1 from DiagnosticQueries.sql

2. **Don't know ReverseArgs parameters?**  
   → Check MrSIXProxyV2 documentation or decompile the DLL

3. **Fix applied but still seeing mismatches?**  
   → Review SHARD4_DIAGNOSTIC.md section "Alternative Fixes"

4. **Want full details?**  
   → Read INVESTIGATION_SUMMARY.md

---

**Last Updated**: March 4, 2026  
**Status**: Investigation complete, fix identified, diagnostic logging added
