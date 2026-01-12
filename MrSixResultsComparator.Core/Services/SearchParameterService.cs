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
    Select
        lg.SearcherUserID, lg.SearchName, lg.WhatIfSearchId, lg.OtherUserId, lg.ClassName, lg.RequestCount, lg.SiteCode, lg.CallID,
        lg.GenderGenderSeek,
		lg.LAge,
		lg.UAge,
		lg.LHeight,
		lg.UHeight,
		lg.PhotosOnly,
        lg.SelfString,        
		lg.SeekString,
        lg.WeightString,
		lg.SearchGeoTypeID,
		lg.CountryCode,
		lg.StateCode,
		lg.CityCode,
		lg.PostalCode,
		lg.Distance,
        lg.Latitude,
        lg.Longitude,
        lg.CallTime,
        sh.ShardID,        
        RowNum = ROW_NUMBER() Over (Partition By lg.SiteCode, lg.ClassName, lg.WhatIfSearchId Order By lg.CallTime Desc)
    From SearchData.dbo.SearchLog lg With (ReadUncommitted)
    Join Mcore.dbo.cfgSiteCodeShards sh With (ReadUncommitted)
        On sh.SiteCode = lg.SiteCode
    Where CallTime Between DateAdd(Hour, -1, GetDate()-1) And DateAdd(Hour, 0, GetDate()-1)
    And sh.ShardID = @ShardId
    And lg.SearcherUserId > 999
    And lg.ReturnedCount > 0
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
