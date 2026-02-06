using Microsoft.Data.SqlClient;
using Dapper;
using Serilog;
using MrSixResultsComparator.Core.Models;

namespace MrSixResultsComparator.Core.Services;

public class SearchLogService
{
    private readonly string _connectionString;

    public SearchLogService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public SearchLogEntry? GetSearchLogEntry(Guid callId, DateTime callTime)
    {
        string query = @"
SELECT 
    CallTime, CallID, SID, SiteCode, URLCode, PlatformID, Searchname, Servername,
    Status, ClassName, HostName, AppName, SearcherUserID, Algorithm, Duration,
    RequestCount, ReturnedCount, AvailableMatches, SearchGeoTypeID, CountryCode,
    StateCode, CityCode, CityCodes, PostalCode, Latitude, Longitude, CBSACode,
    Distance, GenderGenderSeek, Age, LAge, UAge, Height, LHeight, UHeight,
    OtherUserId, ServedByCommonality, SeekString, SelfString, WeightString,
    KeyWord, UseDefaultDistance, SearchCriteriaOptions, IMOnlyMs, SearchBlock,
    PhotosOnly, OnlineNow, SpotlightOnly, SubscriberOnly, CertOnly,
    ParamBag, WhatIfSearchId, ResultBag
FROM SearchData.dbo.SearchLog WITH (NOLOCK)
WHERE CallID = @CallId AND CallTime = @CallTime";

        try
        {
            Log.Information("Loading SearchLog entry for CallId: {CallId}, CallTime: {CallTime}", callId, callTime);
            using (var connection = new SqlConnection(_connectionString))
            {
                var result = connection.QueryFirstOrDefault<SearchLogEntry>(
                    query, 
                    new { CallId = callId, CallTime = callTime }, 
                    commandTimeout: 30);
                
                if (result == null)
                {
                    Log.Warning("No SearchLog entry found for CallId: {CallId}, CallTime: {CallTime}", callId, callTime);
                }
                
                return result;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load SearchLog entry for CallId: {CallId}", callId);
            throw;
        }
    }
}
