using MrSIXProxyV2.ResultsV4;
using MrSIXProxyV2.SearchCriteria;
using MrSixResultsComparator.Core.Configuration;
using MrSixResultsComparator.Core.Models;
using Serilog;

namespace MrSixResultsComparator.Core.Services;

public class OneWayService : ISearchService
{
    private readonly AppConfiguration _config;

    public OneWayService(AppConfiguration config)
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
        var args = new OneWayArgs(
            platformId: 0,
            siteCode: searcher.SiteCode,
            sessionId: _config.SessionGuid,
            genderGenderSeek: searcher.GenderGenderSeek,
            geo: searcher.Geo,
            lAge: searcher.LAge,
            uAge: searcher.UAge,
            lHeight: searcher.LHeight,
            uHeight: searcher.UHeight,
            onlineNow: false,
            photosOnly: searcher.PhotosOnly,
            seekingAnswerIds: searcher.SeekingAnswerIds,
            imOnlyMiliseconds: 0,
            searcherUserId: searcher.SearcherUserId,
            maxRecordsToReturn: searcher.RequestCount,
            shardId: searcher.ShardId)
        {
            PinnedToServername = pinnedToServerName,
            UsersToRemove = utr
        };

        args.ExtensionParams = new List<string>(_config.ExtensionParams);
        
        // Add explain parameter if requested (only for retry/verification runs)
        if (enableExplain)
        {
            args.ExtensionParams.Add("explain");
            Log.Debug("Explain tracking enabled for OneWay on {ServerName}", pinnedToServerName);
        }

        args.DynamicArgs ??= new Dictionary<string, string>();
        args.DynamicArgs["OCallId"] = searcher.CallId.ToString();
        args.TimeOutInSeconds = 10000;
        
        try
        {
            Log.Debug("Executing OneWay on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
            response = MrSIXProxyV2.SearchesV5.OneWay.Execute(args);
            Log.Debug("OneWay completed on {ServerName} for CallId: {CallId}. Result count: {ResultCount}", 
                pinnedToServerName, searcher.CallId, response?.Results?.Count ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "OneWay failed on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
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
