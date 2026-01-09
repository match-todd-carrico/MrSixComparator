using MrSIXProxyV2.Input;
using MrSIXProxyV2.ResultsV4;
using MrSIXProxyV2.SearchCriteria;
using Newtonsoft.Json;
using Serilog;
using MrSixResultsComparator.Models;
using MrSixResultsComparator.Configuration;

namespace MrSixResultsComparator.Services;

public class StackSearchService
{
    private readonly AppConfiguration _config;

    public StackSearchService(AppConfiguration config)
    {
        _config = config;
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteStackSearch(
        SearchParameter searcher, 
        string pinnedToServerName, 
        string? config = null)
    {
        SearchResponse<SearchResultRow>? response = null;

        var utr = new List<int>();
        var args = new RecommendedArgs(
            platformId: 0,
            siteCode: searcher.SiteCode,
            shardId: searcher.ShardId,
            sessionId: _config.SessionGuid,
            searcherUserId: searcher.SearcherUserId,
            maxRecordsToReturn: searcher.RequestCount,
            usersToRemove: utr,
            searchTypeId: searcher.WhatIfSearchId,
            geo: searcher.Geo)
        {
            PinnedToServername = pinnedToServerName
        };

        args.ExtensionParams = _config.ExtensionParams;

        args.DynamicArgs ??= new Dictionary<string, string>();
        args.DynamicArgs["OCallId"] = searcher.CallId.ToString();
        args.TimeOutInSeconds = 10000;
        
        if (config != null)
            args.DynamicArgs["stackOverride"] = JsonConvert.SerializeObject(config);

        try
        {
            Log.Debug("Executing StackSearch on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
            response = MrSIXProxyV2.SearchesV5.StackSearch.Execute(args);
            Log.Debug("StackSearch completed on {ServerName} for CallId: {CallId}. Result count: {ResultCount}", 
                pinnedToServerName, searcher.CallId, response?.Results?.Count ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "StackSearch failed on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
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
}
