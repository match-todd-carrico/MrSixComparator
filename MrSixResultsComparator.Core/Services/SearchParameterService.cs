using Microsoft.Data.SqlClient;
using Dapper;
using MrSIXProxyV2.Input;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

    public List<SearchParameter> GetSearchParameters(int shardId, IReadOnlyCollection<short>? siteCodeFilter = null)
    {
        // Only apply the SiteCode filter if the caller actually specified something; an empty/null
        // collection means "no filter" so we still pull every SiteCode for the shard.
        bool hasSiteCodeFilter = siteCodeFilter != null && siteCodeFilter.Count > 0;
        string siteCodeClause = hasSiteCodeFilter ? "    And lg.SiteCode In @SiteCodes" : string.Empty;

        string query = $@"
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
        RowNum = ROW_NUMBER() Over (Partition By lg.SiteCode, lg.ClassName, lg.WhatIfSearchId, lg.[Algorithm], lg.GenderGenderSeek, lg.SearchGeoTypeID Order By lg.CallTime Desc),
        lg.ParamBag,
        lg.[Algorithm],
        lg.KeyWord 
    From SearchData.dbo.SearchLog lg With (ReadUncommitted)
    Join Mcore.dbo.cfgSiteCodeShards sh With (ReadUncommitted)
        On sh.SiteCode = lg.SiteCode
    Where lg.CallTime Between DateAdd(Hour, -1, GetDate()-1) And DateAdd(Hour, 0, GetDate()-1)
    And sh.ShardID = @ShardId
    And lg.SearcherUserId > 999
    And lg.ReturnedCount > 0
    And lg.Servername not in ('DA1MASC804', 'DA1MASC805')
{siteCodeClause}
)
Select *
From lastFew
Where lastFew.RowNum <= 20
    ";

        try
        {
            if (hasSiteCodeFilter)
            {
                Log.Information("Loading search parameters from database for ShardId: {ShardId}, SiteCodes: {SiteCodes}",
                    shardId, string.Join(",", siteCodeFilter!));
            }
            else
            {
                Log.Information("Loading search parameters from database for ShardId: {ShardId} (all SiteCodes)", shardId);
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                // Dapper expands "In @SiteCodes" to a parameterized IN clause when the value is IEnumerable.
                var parameters = hasSiteCodeFilter
                    ? (object)new { ShardId = shardId, SiteCodes = siteCodeFilter!.ToArray() }
                    : new { ShardId = shardId };

                var results = connection.Query<SearchParameter>(query, parameters, commandTimeout: 60).ToList();
                
                foreach (var param in results)
                {
                    param.Description = $"Site:{param.SiteCode} User:{param.SearcherUserId} CallId:{param.CallId}";
                    TryPopulateGeoFromParamBag(param);
                    TryPopulateStickerIdFromParamBag(param);
                    TryPopulateSourceStackConfigFromParamBag(param);
                    NormalizeClassNameForSticker(param);
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

    private static void TryPopulateGeoFromParamBag(SearchParameter param)
    {
        if (string.IsNullOrEmpty(param.ParamBag))
            return;

        try
        {
            var bag = JObject.Parse(param.ParamBag);
            var geoArgsJson = bag.Value<string>("GeoArgsPassedIn");
            if (string.IsNullOrEmpty(geoArgsJson))
                return;

            var geo = JsonConvert.DeserializeObject<GeoCriteria>(geoArgsJson);

            if ((geo?.GeoSearchTypeId).GetValueOrDefault(0) > 0)
                param.Geo = geo;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse GeoArgsPassedIn from ParamBag for User:{UserId} CallId:{CallId}",
                param.SearcherUserId, param.CallId);
        }
    }

    private static void TryPopulateStickerIdFromParamBag(SearchParameter param)
    {
        if (string.IsNullOrEmpty(param.ParamBag))
            return;

        try
        {
            var bag = JObject.Parse(param.ParamBag);
            var cfgAnswerId = bag.Value<string>("cfgAnswerId");
            if (!string.IsNullOrEmpty(cfgAnswerId) && int.TryParse(cfgAnswerId, out int stickerId))
                param.StickerId = stickerId;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse cfgAnswerId from ParamBag for User:{UserId} CallId:{CallId}",
                param.SearcherUserId, param.CallId);
        }
    }

    private static void TryPopulateSourceStackConfigFromParamBag(SearchParameter param)
    {
        if (string.IsNullOrEmpty(param.ParamBag))
            return;

        try
        {
            var bag = JObject.Parse(param.ParamBag);
            var stackConfig = bag.Value<string>("StackConfig");
            if (!string.IsNullOrEmpty(stackConfig))
                param.SourceStackConfig = stackConfig;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse StackConfig from ParamBag for User:{UserId} CallId:{CallId}",
                param.SearcherUserId, param.CallId);
        }
    }

    // SearchLog logs Sticker-family searches (DatingStickerSearch=64, SocialStickerSearch=65,
    // StickerSearchV2=68) under ClassName="Stack". The MrSIX engine actually routes these via
    // a separate branch (SearchMethods.StickerSearch). Normalize to "Sticker" here so dispatch
    // in ComparisonService stays 1:1 on ClassName.
    private static void NormalizeClassNameForSticker(SearchParameter param)
    {
        if (param.WhatIfSearchId is 64 or 65 or 68)
            param.ClassName = "Sticker";
    }
}
