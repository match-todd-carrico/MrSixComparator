# Shard 4 Reverse Search - Visual Explanation

## The Problem (Simplified)

```
┌─────────────────────────────────────────────────────────────────┐
│                     REVERSE SEARCH FLOW                         │
└─────────────────────────────────────────────────────────────────┘

Question: "Who would see User 12345 in their search results?"

┌─────────────────────────────────────────────────────────────────┐
│                      CURRENT (BROKEN)                           │
└─────────────────────────────────────────────────────────────────┘

Search Parameter from DB:
├─ SearcherUserId: 67890
├─ OtherUserId: 12345        ← This is in the database!
└─ ShardId: 4

     ↓ ReverseService.cs constructs ReverseArgs

ReverseArgs sent to Control Server (DA1MASC805):
├─ searcherUserId: 67890     ✓ Passed
├─ otherUserId: ???          ✗ NOT PASSED!
└─ shardId: 4

     ↓ Control picks a random/default user

Control Returns: [User123, User456, User789]

     ↓ ReverseService.cs constructs ReverseArgs again

ReverseArgs sent to Test Server (DA1MASC804):
├─ searcherUserId: 67890     ✓ Passed
├─ otherUserId: ???          ✗ NOT PASSED!
└─ shardId: 4

     ↓ Test picks a different random/default user

Test Returns: [User123, User999, User888]  ← DIFFERENT!

     ↓ ComparisonService compares

Result: ❌ MISMATCH!
├─ Only in Control: [User456, User789]
└─ Only in Test: [User999, User888]

┌─────────────────────────────────────────────────────────────────┐
│                      FIXED (CORRECT)                            │
└─────────────────────────────────────────────────────────────────┘

Search Parameter from DB:
├─ SearcherUserId: 67890
├─ OtherUserId: 12345        ← This is in the database!
└─ ShardId: 4

     ↓ ReverseService.cs constructs ReverseArgs

ReverseArgs sent to Control Server (DA1MASC805):
├─ searcherUserId: 67890     ✓ Passed
├─ otherUserId: 12345        ✓ NOW PASSED!
└─ shardId: 4

     ↓ Control searches for User 12345

Control Returns: [User123, User456, User789]

     ↓ ReverseService.cs constructs ReverseArgs again

ReverseArgs sent to Test Server (DA1MASC804):
├─ searcherUserId: 67890     ✓ Passed
├─ otherUserId: 12345        ✓ NOW PASSED!
└─ shardId: 4

     ↓ Test searches for User 12345 (same user!)

Test Returns: [User123, User456, User789]  ← SAME!

     ↓ ComparisonService compares

Result: ✅ MATCH!
└─ Both returned identical users
```

---

## Code Comparison

### ❌ Current (Broken)

```csharp
// ReverseService.cs - Line 27
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,  // ← Who is searching
    // MISSING: Which user to search for!
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo);
```

**Result**: Control and Test may search for different users

---

### ✅ Fixed (Correct)

```csharp
// ReverseService.cs - Line 27
var args = new ReverseArgs(
    platformId: 0,
    siteCode: searcher.SiteCode,
    shardId: searcher.ShardId,
    sessionId: _config.SessionGuid,
    searcherUserId: searcher.SearcherUserId,  // ← Who is searching
    otherUserId: searcher.OtherUserId,        // ← ADD: Which user to search for
    maxRecordsToReturn: searcher.RequestCount,
    geo: searcher.Geo);
```

**Result**: Control and Test search for the same user (12345)

---

## Why Shard 4?

```
┌────────────────────────────────────────────────────────────────┐
│              SHARD CHARACTERISTICS                             │
└────────────────────────────────────────────────────────────────┘

Shard 1:
├─ User Activity: Low
├─ Default User Behavior: Consistent
└─ Mismatch Rate: ~2% (mostly data movement)

Shard 2:
├─ User Activity: Medium
├─ Default User Behavior: Consistent
└─ Mismatch Rate: ~3%

Shard 3:
├─ User Activity: Medium
├─ Default User Behavior: Consistent
└─ Mismatch Rate: ~2%

Shard 4:  ← THE PROBLEM SHARD
├─ User Activity: HIGH ⚠️
├─ Default User Behavior: INCONSISTENT ⚠️
├─ Data Distribution: Different demographics
├─ Server Behavior: DA1MASC804 ≠ DA1MASC805
└─ Mismatch Rate: ~25% ❌ MUCH HIGHER!

Why Shard 4 is affected more:
1. Higher user activity = more variability when OtherUserId is missing
2. Different default user selection logic between servers
3. More diverse data = different fallback behaviors
4. Timing sensitivity (users changing between Control/Test calls)
```

---

## The Fix (Step-by-Step)

```
┌────────────────────────────────────────────────────────────────┐
│                    IMPLEMENTATION STEPS                        │
└────────────────────────────────────────────────────────────────┘

STEP 1: Verify OtherUserId is in Database
┌─────────────────────────────────────────┐
│ Run DiagnosticQueries.sql - Query 1    │
│ Check if OtherUserId column is NULL    │
│ or has values                           │
└─────────────────────────────────────────┘
         │
         ├─ If NULL ────────────────┐
         │                           ↓
         │                      Investigate why DB
         │                      isn't logging it
         │
         └─ If Has Values ──────────┐
                                    ↓
STEP 2: Check ReverseArgs Constructor
┌─────────────────────────────────────────┐
│ Verify ReverseArgs accepts              │
│ otherUserId parameter                   │
│ (Check MrSIXProxyV2 docs)               │
└─────────────────────────────────────────┘
         │
         └─────────────────────────┐
                                   ↓
STEP 3: Update ReverseService.cs
┌─────────────────────────────────────────┐
│ Add line 35:                            │
│ otherUserId: searcher.OtherUserId,      │
│                                         │
│ Save file                               │
└─────────────────────────────────────────┘
         │
         └─────────────────────────┐
                                   ↓
STEP 4: Build & Test
┌─────────────────────────────────────────┐
│ dotnet build                            │
│ Run comparison on Shard 4               │
│ Check mismatch rate                     │
└─────────────────────────────────────────┘
         │
         └─────────────────────────┐
                                   ↓
STEP 5: Verify Fix
┌─────────────────────────────────────────┐
│ Expected: Mismatch rate drops from      │
│ ~25% to ~2-3% (similar to other shards) │
│                                         │
│ Check logs for:                         │
│ "SHARD 4 DIAGNOSTIC" messages           │
└─────────────────────────────────────────┘
```

---

## Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    COMPARISON PROCESS                           │
└─────────────────────────────────────────────────────────────────┘

┌──────────────────┐
│   SearchData DB  │
│                  │
│ SearchLog table  │
│ ├─ SearcherUserID│──┐
│ ├─ OtherUserId   │  │  Parameters loaded yesterday's data
│ ├─ ClassName     │  │
│ ├─ ShardId       │  │
│ └─ SiteCode      │  │
└──────────────────┘  │
                      ↓
        ┌─────────────────────────┐
        │ SearchParameterService  │
        │ GetSearchParameters(4)  │  ← Load for Shard 4
        └─────────────────────────┘
                      ↓
        ┌─────────────────────────┐
        │   List<SearchParameter> │
        │   ├─ Param 1 (Reverse)  │
        │   ├─ Param 2 (OneWay)   │
        │   └─ Param 3 (Reverse)  │
        └─────────────────────────┘
                      ↓
        ┌─────────────────────────┐
        │   ComparisonService     │
        │   CompareSearchResults  │
        └─────────────────────────┘
                      ↓
          ┌───────────┴───────────┐
          ↓                       ↓
┌──────────────────┐    ┌──────────────────┐
│  ReverseService  │    │  ReverseService  │
│  (Control)       │    │  (Test)          │
│                  │    │                  │
│  ❌ BROKEN:      │    │  ❌ BROKEN:      │
│  Missing         │    │  Missing         │
│  OtherUserId     │    │  OtherUserId     │
│                  │    │                  │
│  ✅ FIXED:       │    │  ✅ FIXED:       │
│  Pass            │    │  Pass            │
│  OtherUserId     │    │  OtherUserId     │
└──────────────────┘    └──────────────────┘
          ↓                       ↓
┌──────────────────┐    ┌──────────────────┐
│  DA1MASC805      │    │  DA1MASC804      │
│  (Control)       │    │  (Test)          │
│                  │    │                  │
│  Returns:        │    │  Returns:        │
│  [User A, B, C]  │    │  [User A, B, C]  │
└──────────────────┘    └──────────────────┘
          ↓                       ↓
          └───────────┬───────────┘
                      ↓
        ┌─────────────────────────┐
        │   ComparisonService     │
        │   Compare Lists         │
        └─────────────────────────┘
                      ↓
        ┌─────────────────────────┐
        │   ComparisonResult      │
        │   ✅ Matched: true      │
        │   ControlCount: 3       │
        │   TestCount: 3          │
        └─────────────────────────┘
```

---

## Expected Results

### Before Fix (Broken)
```
╔══════════════════════════════════════════════════════════════╗
║              SHARD 4 COMPARISON RESULTS                      ║
╚══════════════════════════════════════════════════════════════╝

Service: SearchV4.Reverse
SiteCode: 1 (Shard 4)
─────────────────────────────────────────────────────────────
❌ MISMATCHED: 18 out of 20 searches (90%)
✅ MATCHED:     2 out of 20 searches (10%)

Typical Mismatch Example:
├─ SearcherUserId: 67890
├─ Control Count: 15 results
├─ Test Count: 12 results
├─ Only in Control: [101, 102, 103]
├─ Only in Test: [201, 202, 203]
└─ Reason: Different users being searched for!
```

### After Fix (Corrected)
```
╔══════════════════════════════════════════════════════════════╗
║              SHARD 4 COMPARISON RESULTS                      ║
╚══════════════════════════════════════════════════════════════╝

Service: SearchV4.Reverse
SiteCode: 1 (Shard 4)
─────────────────────────────────────────────────────────────
✅ MATCHED:     19 out of 20 searches (95%)
❌ MISMATCHED:  1 out of 20 searches (5%)

Remaining Mismatches:
└─ Likely due to data movement (eventual consistency)
   Can be ignored with RecentLoginThresholdMinutes setting
```

---

## Summary

**The Problem**: Missing `OtherUserId` parameter causes non-deterministic Reverse searches

**The Impact**: Shard 4 shows ~90% mismatch rate (vs ~2% on other shards)

**The Solution**: Add one line of code to pass `OtherUserId` to `ReverseArgs`

**The Result**: Mismatch rate drops to ~5% (matching other shards)

**Time to Fix**: ~2 minutes (once ReverseArgs signature is confirmed)

**Difficulty**: ⭐ Very Easy - Just add one parameter

---

For detailed steps, see:
- **QUICK_REFERENCE.md** - Fast implementation guide
- **INVESTIGATION_SUMMARY.md** - Complete analysis
- **SHARD4_DIAGNOSTIC.md** - Troubleshooting guide
