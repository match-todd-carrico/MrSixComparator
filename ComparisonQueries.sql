-- ============================================================================
-- SHARD 0 vs SHARD 4 COMPARATIVE ANALYSIS
-- ============================================================================
-- These queries compare shard 0 (working) vs shard 4 (broken) to identify
-- the specific difference causing inaccurate comparisons

-- ============================================================================
-- Query A: OtherUserId Population Comparison
-- ============================================================================
-- Key Question: Does shard 4 have more NULL OtherUserId values than shard 0?

SELECT 
    sh.ShardID,
    COUNT(*) as TotalReverseSearches,
    SUM(CASE WHEN lg.OtherUserId IS NULL THEN 1 ELSE 0 END) as NullCount,
    SUM(CASE WHEN lg.OtherUserId IS NOT NULL THEN 1 ELSE 0 END) as PopulatedCount,
    CAST(100.0 * SUM(CASE WHEN lg.OtherUserId IS NULL THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,2)) as PercentNull,
    AVG(lg.OtherUserId) as AvgOtherUserId
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
    AND lg.ReturnedCount > 0
    AND sh.ShardID IN (0, 4)  -- Compare only shard 0 and 4
GROUP BY sh.ShardID
ORDER BY sh.ShardID;

/* Expected Results:
   - If PercentNull is similar (both ~0% or both ~100%): OtherUserId NOT the issue
   - If Shard 4 has higher PercentNull: OtherUserId is the problem
   - If both have high PercentNull but shard 0 works: Server handles it differently
*/

-- ============================================================================
-- Query B: Server Behavior Comparison by Shard
-- ============================================================================
-- Key Question: Do DA1MASC804 and DA1MASC805 behave differently on shard 4?

SELECT 
    sh.ShardID,
    lg.Servername,
    COUNT(*) as SearchCount,
    COUNT(DISTINCT lg.SearcherUserID) as UniqueSearchers,
    AVG(lg.ReturnedCount) as AvgResults,
    STDEV(lg.ReturnedCount) as StdDevResults,  -- High = inconsistent behavior
    MIN(lg.ReturnedCount) as MinResults,
    MAX(lg.ReturnedCount) as MaxResults,
    AVG(lg.Duration) as AvgDurationMs
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
    AND lg.ReturnedCount > 0
    AND sh.ShardID IN (0, 4)
GROUP BY sh.ShardID, lg.Servername
ORDER BY sh.ShardID, lg.Servername;

/* Expected Results:
   - Shard 0: DA1MASC804 and DA1MASC805 should be similar
   - Shard 4: Look for big differences between the two servers
   - High StdDev = inconsistent/variable behavior
*/

-- ============================================================================
-- Query C: User Activity Level Comparison
-- ============================================================================
-- Key Question: Is shard 4 more active/variable than shard 0?

SELECT 
    sh.ShardID,
    COUNT(DISTINCT lg.SearcherUserID) as UniqueSearchers,
    COUNT(DISTINCT lg.OtherUserId) as UniqueOtherUsers,
    COUNT(*) as TotalSearches,
    CAST(1.0 * COUNT(*) / NULLIF(COUNT(DISTINCT lg.SearcherUserID), 0) AS DECIMAL(10,2)) as SearchesPerSearcher,
    CAST(1.0 * COUNT(*) / NULLIF(COUNT(DISTINCT lg.OtherUserId), 0) AS DECIMAL(10,2)) as SearchesPerOtherUser,
    MIN(lg.CallTime) as EarliestSearch,
    MAX(lg.CallTime) as LatestSearch
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
    AND sh.ShardID IN (0, 4)
GROUP BY sh.ShardID
ORDER BY sh.ShardID;

/* Expected Results:
   - If shard 4 has much higher SearchesPerSearcher: More active users
   - If shard 4 has more UniqueOtherUsers: More diverse targets
   - Higher activity = more potential for variability
*/

-- ============================================================================
-- Query D: Site Code Distribution by Shard
-- ============================================================================
-- Key Question: Are different site codes on shard 0 vs 4?

SELECT 
    sh.ShardID,
    sh.SiteCode,
    COUNT(*) as ReverseSearchCount,
    COUNT(DISTINCT lg.SearcherUserID) as UniqueUsers,
    AVG(lg.ReturnedCount) as AvgResults,
    SUM(CASE WHEN lg.OtherUserId IS NULL THEN 1 ELSE 0 END) as NullOtherUserIdCount
FROM Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
LEFT JOIN SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
    ON lg.SiteCode = sh.SiteCode
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
WHERE sh.ShardID IN (0, 4)
GROUP BY sh.ShardID, sh.SiteCode
HAVING COUNT(*) > 0  -- Only show site codes with activity
ORDER BY sh.ShardID, ReverseSearchCount DESC;

/* Expected Results:
   - Completely different site codes on each shard
   - Site-specific behavior might explain differences
   - Some sites might have different configurations
*/

-- ============================================================================
-- Query E: Result Count Distribution
-- ============================================================================
-- Key Question: Are result counts more variable on shard 4?

WITH ResultDistribution AS (
    SELECT 
        sh.ShardID,
        lg.ReturnedCount,
        COUNT(*) as Frequency
    FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
    JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
        ON sh.SiteCode = lg.SiteCode
    WHERE lg.ClassName = 'SearchV4.Reverse'
        AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
        AND sh.ShardID IN (0, 4)
    GROUP BY sh.ShardID, lg.ReturnedCount
)
SELECT 
    ShardID,
    AVG(ReturnedCount) as AvgReturnedCount,
    MIN(ReturnedCount) as MinReturnedCount,
    MAX(ReturnedCount) as MaxReturnedCount,
    STDEV(ReturnedCount) as StdDevReturnedCount,
    COUNT(DISTINCT ReturnedCount) as UniqueResultCounts
FROM ResultDistribution
GROUP BY ShardID
ORDER BY ShardID;

/* Expected Results:
   - Higher StdDev on shard 4 = more variability
   - More UniqueResultCounts = less predictable
   - This could indicate non-deterministic behavior
*/

-- ============================================================================
-- Query F: Sample Searches Side-by-Side
-- ============================================================================
-- Key Question: What do actual search parameters look like on each shard?

-- Shard 0 Examples
SELECT TOP 5
    'Shard 0' as Label,
    lg.CallTime,
    lg.CallID,
    lg.SearcherUserID,
    lg.OtherUserId,
    lg.SiteCode,
    lg.RequestCount,
    lg.ReturnedCount,
    lg.Servername,
    lg.Duration
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE sh.ShardID = 0
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -2, GETDATE())
    AND lg.ReturnedCount > 0
ORDER BY lg.CallTime DESC;

-- Shard 4 Examples
SELECT TOP 5
    'Shard 4' as Label,
    lg.CallTime,
    lg.CallID,
    lg.SearcherUserID,
    lg.OtherUserId,
    lg.SiteCode,
    lg.RequestCount,
    lg.ReturnedCount,
    lg.Servername,
    lg.Duration
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE sh.ShardID = 4
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -2, GETDATE())
    AND lg.ReturnedCount > 0
ORDER BY lg.CallTime DESC;

/* Expected Results:
   - Compare OtherUserId values (NULL vs populated)
   - Compare RequestCount and ReturnedCount
   - Look for patterns that differ
*/

-- ============================================================================
-- Query G: Server Consistency Check
-- ============================================================================
-- Key Question: Does the same user get consistent results from both servers?

SELECT 
    sh.ShardID,
    lg.SearcherUserID,
    lg.OtherUserId,
    lg.SiteCode,
    COUNT(DISTINCT lg.Servername) as ServerCount,
    COUNT(DISTINCT lg.ReturnedCount) as DifferentResultCounts,
    MIN(lg.ReturnedCount) as MinResults,
    MAX(lg.ReturnedCount) as MaxResults,
    STRING_AGG(CAST(lg.Servername AS NVARCHAR(MAX)), ', ') as Servers
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
    AND sh.ShardID IN (0, 4)
GROUP BY sh.ShardID, lg.SearcherUserID, lg.OtherUserId, lg.SiteCode
HAVING COUNT(DISTINCT lg.Servername) > 1  -- User searched by multiple servers
   AND COUNT(DISTINCT lg.ReturnedCount) > 1  -- Got different result counts
ORDER BY sh.ShardID, DifferentResultCounts DESC;

/* Expected Results:
   - Shard 0: Should have few/no rows (consistent results)
   - Shard 4: May have many rows (inconsistent between servers)
   - This directly shows server behavior differences
*/

-- ============================================================================
-- Query H: Time-Based Analysis
-- ============================================================================
-- Key Question: Are results more inconsistent at certain times?

SELECT 
    sh.ShardID,
    DATEPART(HOUR, lg.CallTime) as HourOfDay,
    COUNT(*) as SearchCount,
    AVG(lg.ReturnedCount) as AvgResults,
    STDEV(lg.ReturnedCount) as StdDevResults,
    COUNT(DISTINCT lg.SearcherUserID) as UniqueUsers
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
    AND sh.ShardID IN (0, 4)
GROUP BY sh.ShardID, DATEPART(HOUR, lg.CallTime)
ORDER BY sh.ShardID, HourOfDay;

/* Expected Results:
   - Look for peak hours on each shard
   - Higher StdDev during busy times = timing/load issues
   - Pattern differences between shards
*/

-- ============================================================================
-- INTERPRETATION GUIDE
-- ============================================================================
-- 
-- Query A (OtherUserId):
--   - Both shards similar % NULL → Not the issue
--   - Shard 4 higher % NULL → OtherUserId population problem
--   - Both high % NULL but shard 0 works → Server handles it differently
--
-- Query B (Server Behavior):
--   - Look for StdDev differences between DA1MASC804 and DA1MASC805 on shard 4
--   - If one server has much different AvgResults → Configuration difference
--   - Compare this to shard 0 (should be consistent)
--
-- Query C (Activity Level):
--   - Higher SearchesPerSearcher on shard 4 → More active users
--   - More UniqueOtherUsers → More diverse search targets
--   - This could explain higher variability
--
-- Query D (Site Codes):
--   - Different site codes → Site-specific behavior
--   - Some sites may have different configurations
--   - Check if problematic sites are all on shard 4
--
-- Query G (Consistency):
--   - This is THE KEY QUERY
--   - Shows users getting different results from different servers
--   - If shard 4 has many rows but shard 0 has few → Server config issue
--
-- ============================================================================
-- NEXT STEPS BASED ON RESULTS
-- ============================================================================
--
-- If Query A shows both NULL:
--   → Missing OtherUserId is likely the root cause (fix ReverseService)
--   → But investigate why shard 0 works despite this
--
-- If Query B shows server differences on shard 4:
--   → Check server configurations (feature flags, algorithm versions)
--   → Look at: http://DA1MASC804:8888/admin/getEngineStatus
--   → Look at: http://DA1MASC805:8888/admin/getEngineStatus
--
-- If Query G shows inconsistency on shard 4:
--   → Confirms server behavior difference
--   → Focus on aligning server configurations
--
-- If Query D shows different site codes:
--   → Test with same site code on both shards
--   → May need site-specific handling
--
-- ============================================================================
