using Microsoft.Data.SqlClient;
using Dapper;
using Serilog;
using MrSixResultsComparator.Core.Models;

namespace MrSixResultsComparator.Core.Services;

public class SearchParameterService
{
    private readonly string _connectionString;

    public SearchParameterService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public List<SearchParameter> GetSearchParameters(int shardId)
    {
        string query = @"
            ;With lastFew As (
        Select -- Top(100)
            sl.SiteCode
            ,ss.ShardId
            ,sl.SearcherUserId
            ,sl.RequestCount
            ,sl.WhatIfSearchId
            ,sl.CallId
            ,RowNum = ROW_NUMBER() Over (Partition By ss.SiteCode Order By sl.CallTime Desc)
            ,sl.CallTime
        From SearchData.dbo.SearchLog sl With (ReadUncommitted)
        Join Mcore.dbo.cfgSiteCodeShards ss With (ReadUncommitted)
            On ss.SiteCode = sl.SiteCode
        Where CallTime Between DateAdd(Hour, -1, GetDate()-1) And DateAdd(Hour, 0, GetDate()-1)
        And ss.ShardID = @ShardId
        And sl.SearcherUserId > 999
        And sl.ReturnedCount > 0
    )
    Select *
    From lastFew
    Where lastFew.RowNum <= 20
    ";

        try
        {
            Log.Information("Loading search parameters from database for ShardId: {ShardId}", shardId);
            using (var connection = new SqlConnection(_connectionString))
            {
                var results = connection.Query<SearchParameter>(query, new { ShardId = shardId }, commandTimeout: 60).ToList();
                
                // Set descriptions for each parameter
                foreach (var param in results)
                {
                    param.Description = $"Site:{param.SiteCode} User:{param.SearcherUserId} CallId:{param.CallId}";
                }
                
                Log.Information("Loaded {Count} search parameters from database", results.Count);
                return results;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load search parameters from database");
            throw;
        }
    }
}
