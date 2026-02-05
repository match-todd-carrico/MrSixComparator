using MrSIXProxyV2.Input;
using MrSIXProxyV2.ResultsV4;
using MrSIXProxyV2.SearchCriteria;
using Newtonsoft.Json;
using Serilog;
using MrSixResultsComparator.Core.Models;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.Core.Services;

public class OnePushService : ISearchService
{
    private readonly AppConfiguration _config;

    public OnePushService(AppConfiguration config)
    {
        _config = config;
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteSearch(
        SearchParameter searcher, 
        string pinnedToServerName, 
        string? config = null,
        bool enableExplain = false)
    {
        return ExecuteOnePushSearch(searcher, pinnedToServerName, config, enableExplain);
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteOnePushSearch(
        SearchParameter searcher, 
        string pinnedToServerName, 
        string? config = null,
        bool enableExplain = false)
    {
        SearchResponse<SearchResultRow>? response = null;

        var args = new OnePushArgs(
            platformId: 0,
            siteCode: searcher.SiteCode,
            shardId: searcher.ShardId,
            sessionId: _config.SessionGuid,
            searcherUserId: searcher.SearcherUserId,
            maxRecordsToReturn: searcher.RequestCount,
            geo: searcher.Geo)
        {
            PinnedToServername = pinnedToServerName
        };

        args.ExtensionParams = new List<string>(_config.ExtensionParams);
        args.ExtensionParams.Add("DoNotRandomizeMorePie");
        
        // Add explain parameter if requested (only for retry/verification runs)
        if (enableExplain)
        {
            args.ExtensionParams.Add("explain");
            Log.Debug("Explain tracking enabled for OnePush on {ServerName}", pinnedToServerName);
        }

        args.DynamicArgs ??= new Dictionary<string, string>();
        args.DynamicArgs["OCallId"] = searcher.CallId.ToString();
        args.TimeOutInSeconds = 10000;
        
        try
        {
            Log.Debug("Executing OnePush on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
            response = MrSIXProxyV2.SearchesV5.OnePush.Execute(args);
            Log.Debug("OnePush completed on {ServerName} for CallId: {CallId}. Result count: {ResultCount}", 
                pinnedToServerName, searcher.CallId, response?.Results?.Count ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OnePush failed on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
            throw;
        }

        return Task.FromResult(response!);
    }

    public List<int> ExtractUserIds(SearchResponse<SearchResultRow> response)
    {
        if (response?.Results == null)
            return new List<int>();
        
        return response.Results.Select(r => r.UserId).ToList();
    }

    public Dictionary<string, List<int>> ExtractUserIdsBySlotType(SearchResponse<SearchResultRow> response)
    {
        var result = new Dictionary<string, List<int>>();
        
        if (response?.Results == null)
            return result;
        
        foreach (var row in response.Results)
        {
            var slotType = row.ResultSlotType.ToString();
            
            if (!result.ContainsKey(slotType))
                result[slotType] = new List<int>();
            
            result[slotType].Add(row.UserId);
        }
        
        return result;
    }
}
