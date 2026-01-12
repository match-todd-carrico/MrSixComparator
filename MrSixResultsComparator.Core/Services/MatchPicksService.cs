using Azure;
using MrSIXProxyV2.Input;
using MrSIXProxyV2.ResultsV4;
using MrSIXProxyV2.SearchCriteria;
using MrSixResultsComparator.Core.Configuration;
using MrSixResultsComparator.Core.Models;
using Serilog;

namespace MrSixResultsComparator.Core.Services;

public class MatchPicksService : ISearchService
{
    private readonly AppConfiguration _config;

    public MatchPicksService(AppConfiguration config)
    {
        _config = config;
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteSearch(
        SearchParameter searcher, 
        string pinnedToServerName, 
        string? config = null)
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
        
        args.DynamicArgs ??= new Dictionary<string, string>();
        args.DynamicArgs["OCallId"] = searcher.CallId.ToString();
        args.TimeOutInSeconds = 10000;
        
        try
        {
            Log.Debug("Executing MatchPicks on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
            response = MrSIXProxyV2.SearchesV5.Recommended.Execute(args);
            Log.Debug("MatchPicks completed on {ServerName} for CallId: {CallId}. Result count: {ResultCount}", 
                pinnedToServerName, searcher.CallId, response?.Results?.Count ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MatchPicks failed on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
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
