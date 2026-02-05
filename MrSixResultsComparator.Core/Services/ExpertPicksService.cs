using MrSIXProxyV2.Input;
using MrSIXProxyV2.ResultsV4;
using MrSIXProxyV2.SearchCriteria;
using Serilog;
using MrSixResultsComparator.Core.Models;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.Core.Services;

public class ExpertPicksService : ISearchService
{
    private readonly AppConfiguration _config;

    public ExpertPicksService(AppConfiguration config)
    {
        _config = config;
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteSearch(
        SearchParameter searcher, 
        string pinnedToServerName, 
        string? config = null,
        bool enableExplain = false)
    {
        SearchResponse<SearchResultRow>? response = null;

        var utr = new List<int>();
        var args = new RecommendedArgs(
            platformId: 0,
            siteCode: searcher.SiteCode,
            sessionId: _config.SessionGuid,
            searcherUserId: searcher.SearcherUserId,
            maxRecordsToReturn: searcher.RequestCount,
            usersToRemove: utr,
            searchTypeId: searcher.WhatIfSearchId,
            geo: searcher.Geo,
            shardId: searcher.ShardId)
        {
            PinnedToServername = pinnedToServerName
        };

        args.ExtensionParams = new List<string>(_config.ExtensionParams);
        
        // Add explain parameter if requested (only for retry/verification runs)
        if (enableExplain)
        {
            args.ExtensionParams.Add("explain");
            Log.Debug("Explain tracking enabled for ExpertPicks on {ServerName}", pinnedToServerName);
        }
        
        args.DynamicArgs ??= new Dictionary<string, string>();
        args.DynamicArgs["OCallId"] = searcher.CallId.ToString();
        args.TimeOutInSeconds = 10000;
        
        try
        {
            Log.Debug("Executing ExpertPicks on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
            response = MrSIXProxyV2.SearchesV5.Recommended.Execute(args);
            Log.Debug("ExpertPicks completed on {ServerName} for CallId: {CallId}. Result count: {ResultCount}", 
                pinnedToServerName, searcher.CallId, response?.Results?.Count ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ExpertPicks failed on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
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
