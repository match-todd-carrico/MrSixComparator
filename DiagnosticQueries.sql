-- ============================================================================
-- SHARD 4 REVERSE SEARCH DIAGNOSTIC QUERIES
-- ============================================================================
-- These queries help diagnose why shard 4 reverse search comparisons are inaccurate
-- Run these against the SearchData database

-- ============================================================================
-- Query 1: Check OtherUserId Population for Reverse Searches on Shard 4
-- ============================================================================
-- This checks if Reverse searches are logging the OtherUserId parameter
-- If OtherUserId is NULL, it explains why comparisons might be inconsistent

SELECT TOP 100
    lg.CallTime,
    lg.CallID,
    lg.SearcherUserID,
    lg.OtherUserId,              -- KEY: Check if this is NULL or populated
    lg.ClassName,
    lg.SiteCode,
    lg.RequestCount,
    lg.ReturnedCount,
    sh.ShardID,
    lg.Servername
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE sh.ShardID = 4
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
    AND lg.ReturnedCount > 0
ORDER BY lg.CallTime DESC;

-- ============================================================================
-- Query 2: OtherUserId Statistics by Shard
-- ============================================================================
-- Compare OtherUserId population across all shards for Reverse searches

SELECT 
    sh.ShardID,
    COUNT(*) as TotalReverseSearches,
    SUM(CASE WHEN lg.OtherUserId IS NULL THEN 1 ELSE 0 END) as NullOtherUserId,
    SUM(CASE WHEN lg.OtherUserId IS NOT NULL THEN 1 ELSE 0 END) as PopulatedOtherUserId,
    CAST(100.0 * SUM(CASE WHEN lg.OtherUserId IS NULL THEN 1 ELSE 0 END) / COUNT(*) AS DECIMAL(5,2)) as PercentNull
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
    AND lg.ReturnedCount > 0
GROUP BY sh.ShardID
ORDER BY sh.ShardID;

-- ============================================================================
-- Query 3: Site Code Distribution on Shard 4
-- ============================================================================
-- Shows which site codes are on shard 4 and their Reverse search activity

SELECT 
    sh.SiteCode,
    sh.ShardID,
    COUNT(DISTINCT lg.SearcherUserID) as UniqueSearchers,
    COUNT(*) as TotalReverseSearches,
    AVG(lg.ReturnedCount) as AvgReturnedCount,
    MIN(lg.CallTime) as EarliestSearch,
    MAX(lg.CallTime) as LatestSearch
FROM Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
LEFT JOIN SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
    ON lg.SiteCode = sh.SiteCode
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
WHERE sh.ShardID = 4
GROUP BY sh.SiteCode, sh.ShardID
ORDER BY TotalReverseSearches DESC;

-- ============================================================================
-- Query 4: Compare Shard 4 vs Other Shards (Reverse Search Characteristics)
-- ============================================================================
-- Identifies unique characteristics of shard 4 that might affect comparisons

SELECT 
    sh.ShardID,
    COUNT(*) as TotalSearches,
    COUNT(DISTINCT lg.SiteCode) as UniqueSiteCodes,
    COUNT(DISTINCT lg.SearcherUserID) as UniqueSearchers,
    AVG(lg.ReturnedCount) as AvgResults,
    AVG(lg.Duration) as AvgDurationMs,
    MAX(lg.ReturnedCount) as MaxResults,
    MIN(lg.ReturnedCount) as MinResults
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
    AND lg.ReturnedCount > 0
GROUP BY sh.ShardID
ORDER BY sh.ShardID;

-- ============================================================================
-- Query 5: Sample Reverse Search Parameters for Testing
-- ============================================================================
-- Gets the exact query used by SearchParameterService to load test data
-- Use this to see what would be loaded for comparisons

WITH lastFew AS (
    SELECT
        lg.SearcherUserID, lg.SearchName, lg.WhatIfSearchId, lg.OtherUserId, 
        lg.ClassName, lg.RequestCount, lg.SiteCode, lg.CallID,
        lg.GenderGenderSeek,
        lg.LAge, lg.UAge, lg.LHeight, lg.UHeight, lg.PhotosOnly,
        lg.SelfString, lg.SeekString, lg.WeightString,
        lg.SearchGeoTypeID, lg.CountryCode, lg.StateCode, lg.CityCode,
        lg.PostalCode, lg.Distance, lg.Latitude, lg.Longitude,
        lg.CallTime,
        sh.ShardID,
        RowNum = ROW_NUMBER() OVER (
            PARTITION BY lg.SiteCode, lg.ClassName, lg.WhatIfSearchId 
            ORDER BY lg.CallTime DESC
        )
    FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
    JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
        ON sh.SiteCode = lg.SiteCode
    WHERE CallTime BETWEEN DATEADD(HOUR, -1, GETDATE()-1) AND DATEADD(HOUR, 0, GETDATE()-1)
        AND sh.ShardID = 4  -- Shard 4 specifically
        AND lg.SearcherUserId > 999
        AND lg.ReturnedCount > 0
        AND lg.ClassName = 'SearchV4.Reverse'
)
SELECT *
FROM lastFew
WHERE lastFew.RowNum <= 20
ORDER BY ClassName, SiteCode, SearcherUserID;

-- ============================================================================
-- Query 6: Recent Login Activity on Shard 4
-- ============================================================================
-- Checks if shard 4 has more users with recent LastLoginDate changes
-- This could indicate more data movement and eventual consistency issues

-- Note: This requires access to user tables which may not be in SearchData
-- Adjust table names based on your schema
/*
SELECT 
    sh.ShardID,
    COUNT(DISTINCT u.UserID) as TotalUsers,
    SUM(CASE WHEN u.LastLoginDate > DATEADD(MINUTE, -60, GETDATE()) THEN 1 ELSE 0 END) as RecentLogins_60min,
    SUM(CASE WHEN u.LastLoginDate > DATEADD(MINUTE, -120, GETDATE()) THEN 1 ELSE 0 END) as RecentLogins_120min,
    CAST(100.0 * SUM(CASE WHEN u.LastLoginDate > DATEADD(MINUTE, -60, GETDATE()) THEN 1 ELSE 0 END) / COUNT(DISTINCT u.UserID) AS DECIMAL(5,2)) as PercentRecent_60min
FROM [UserDatabase].[dbo].[Users] u
JOIN Mcore.dbo.cfgSiteCodeShards sh
    ON sh.SiteCode = u.SiteCode
GROUP BY sh.ShardID
ORDER BY sh.ShardID;
*/

-- ============================================================================
-- Query 7: Server Distribution for Shard 4 Reverse Searches
-- ============================================================================
-- Checks which servers are handling shard 4 reverse searches
-- Helps identify if there's server-specific behavior

SELECT 
    lg.Servername,
    COUNT(*) as SearchCount,
    COUNT(DISTINCT lg.SearcherUserID) as UniqueUsers,
    AVG(lg.ReturnedCount) as AvgResults,
    AVG(lg.Duration) as AvgDurationMs,
    MIN(lg.CallTime) as FirstSearch,
    MAX(lg.CallTime) as LastSearch
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE sh.ShardID = 4
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
GROUP BY lg.Servername
ORDER BY SearchCount DESC;

-- ============================================================================
-- Query 8: Find Specific Mismatches (If Available)
-- ============================================================================
-- If you have logged mismatches, check their OtherUserId values
-- Replace with actual mismatched SearcherUserIds you're seeing

/*
DECLARE @MismatchedSearcherUserId INT = 123456;  -- Replace with actual mismatched UserID
DECLARE @ShardId INT = 4;

SELECT 
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
WHERE lg.SearcherUserID = @MismatchedSearcherUserId
    AND sh.ShardID = @ShardId
    AND lg.ClassName = 'SearchV4.Reverse'
    AND lg.CallTime > DATEADD(DAY, -7, GETDATE())
ORDER BY lg.CallTime DESC;
*/

-- ============================================================================
-- Query 9: All Search Services on Shard 4 (For Comparison)
-- ============================================================================
-- Shows if Reverse has unique patterns compared to other search types on shard 4

SELECT 
    lg.ClassName,
    COUNT(*) as TotalSearches,
    COUNT(DISTINCT lg.SearcherUserID) as UniqueSearchers,
    SUM(CASE WHEN lg.OtherUserId IS NULL THEN 1 ELSE 0 END) as NullOtherUserId,
    SUM(CASE WHEN lg.OtherUserId IS NOT NULL THEN 1 ELSE 0 END) as PopulatedOtherUserId,
    AVG(lg.ReturnedCount) as AvgResults,
    AVG(lg.Duration) as AvgDurationMs
FROM SearchData.dbo.SearchLog lg WITH (ReadUncommitted)
JOIN Mcore.dbo.cfgSiteCodeShards sh WITH (ReadUncommitted)
    ON sh.SiteCode = lg.SiteCode
WHERE sh.ShardID = 4
    AND lg.CallTime > DATEADD(HOUR, -24, GETDATE())
    AND lg.ReturnedCount > 0
GROUP BY lg.ClassName
ORDER BY lg.ClassName;

-- ============================================================================
-- INTERPRETATION GUIDE
-- ============================================================================
-- 
-- Query 1 Results:
-- - If OtherUserId is NULL for most/all rows: This is the PRIMARY ISSUE
--   → The ReverseService needs to be fixed to pass OtherUserId
-- - If OtherUserId is populated: Look at other diagnostic queries
--
-- Query 2 Results:
-- - If shard 4 has significantly higher PercentNull than other shards:
--   → Shard-specific data issue or logging problem
-- - If all shards have NULL OtherUserId: Application-wide issue
--
-- Query 4 Results:
-- - If shard 4 has notably different AvgResults or AvgDurationMs:
--   → Data characteristics differ, may need shard-specific tuning
-- - If MaxResults is very high: May have hot users causing variability
--
-- Query 7 Results:
-- - If DA1MASC804 and DA1MASC805 show different patterns:
--   → Server configuration difference is contributing to mismatches
-- - If multiple servers serve shard 4: Load balancing may cause inconsistency
--
-- ============================================================================
