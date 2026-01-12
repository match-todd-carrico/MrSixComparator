using Azure;
using MrSIXProxyV2.Input;
using MrSIXProxyV2.ResultsV4;
using MrSIXProxyV2.SearchCriteria;
using MrSixResultsComparator.Core.Configuration;
using MrSixResultsComparator.Core.Models;
using Serilog;

namespace MrSixResultsComparator.Core.Services;

public class SearchWowService : ISearchService
{
    private readonly AppConfiguration _config;

    public SearchWowService(AppConfiguration config)
    {
        _config = config;
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteSearch(
        SearchParameter searcher, 
        string pinnedToServerName, 
        string? config = null)
    {
        SearchResponse<SearchResultRow>? response = null;

        var args = new SearchWowArgs(
            platformId: 0,
            siteCode: searcher.SiteCode,
            shardId: searcher.ShardId,
            sessionId: _config.SessionGuid,
            searcherUserId: searcher.SearcherUserId,
            maxRecordsNewMember: 5,
            maxRecordsPopular: 5,
            genderGenderSeek: searcher.GenderGenderSeek,
            geo: null,
            age: (byte)(((searcher.UAge - searcher.LAge) / 2) + searcher.LAge),
            lAge: searcher.LAge,
            uAge: searcher.UAge,
            height: (short)(((searcher.UHeight.GetValueOrDefault(0) - searcher.LHeight.GetValueOrDefault(0)) / 2) + searcher.LHeight.GetValueOrDefault(0)),
            lHeight: searcher.LHeight,
            uHeight: searcher.UHeight,
            selfAnswerIds: searcher.SelfAnswerIds,
            seekingAnswerIds: searcher.SeekingAnswerIds,
            seekingAttributeWeights: searcher.SeekingAttributeWeights
        )
        {
            PinnedToServername = pinnedToServerName
        };

        args.ExtensionParams = new List<string>(_config.ExtensionParams);

        args.DynamicArgs ??= new Dictionary<string, string>();
        args.DynamicArgs["OCallId"] = searcher.CallId.ToString();
        args.TimeOutInSeconds = 10000;
        
        try
        {
            Log.Debug("Executing SearchWow on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
            response = MrSIXProxyV2.SearchesV5.SearchWow.Execute(args);
            Log.Debug("SearchWow completed on {ServerName} for CallId: {CallId}. Result count: {ResultCount}", 
                pinnedToServerName, searcher.CallId, response?.Results?.Count ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "SearchWow failed on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
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
