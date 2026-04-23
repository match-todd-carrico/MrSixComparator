using MrSIXProxyV2.Input;
using MrSIXProxyV2.ResultsV4;
using MrSIXProxyV2.SearchCriteria;
using Serilog;
using MrSixResultsComparator.Core.Models;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.Core.Services;

// Handles SearchV4 sticker-family searches (DatingStickerSearch=64, SocialStickerSearch=65,
// StickerSearchV2=68). SearchParameterService normalizes these rows to ClassName="Sticker".
// Mirrors StackSearchService but calls MrSIXProxyV2.SearchesV5.StickerSearch and sets
// RecommendedArgs.StickerId from SearchParameter.StickerId (from ParamBag.cfgAnswerId).
public class StickerSearchService : ISearchService
{
    private readonly AppConfiguration _config;

    public StickerSearchService(AppConfiguration config)
    {
        _config = config;
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteSearch(
        SearchParameter searcher,
        string pinnedToServerName,
        string? config = null,
        bool enableExplain = false)
    {
        return ExecuteStickerSearch(searcher, pinnedToServerName, config, enableExplain);
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteStickerSearch(
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
            shardId: searcher.ShardId,
            sessionId: _config.SessionGuid,
            searcherUserId: searcher.SearcherUserId,
            maxRecordsToReturn: searcher.RequestCount,
            usersToRemove: utr,
            searchTypeId: searcher.WhatIfSearchId,
            geo: searcher.Geo)
        {
            PinnedToServername = pinnedToServerName,
            StickerId = searcher.StickerId
        };

        args.ExtensionParams = new List<string>(_config.ExtensionParams);
        args.ExtensionParams.Add("doNotRandom");
        args.ExtensionParams.Add("SingleThread");

        if (enableExplain)
        {
            args.ExtensionParams.Add("explain");
            Log.Debug("Explain tracking enabled for StickerSearch on {ServerName}", pinnedToServerName);
        }

        args.DynamicArgs ??= new Dictionary<string, string>();
        args.DynamicArgs["OCallId"] = searcher.CallId.ToString();
        args.TimeOutInSeconds = 10000;

        try
        {
            Log.Debug("Executing StickerSearch on {ServerName} for CallId: {CallId}, WhatIfSearchId: {WhatIfSearchId}, StickerId: {StickerId}",
                pinnedToServerName, searcher.CallId, searcher.WhatIfSearchId, searcher.StickerId);
            response = MrSIXProxyV2.SearchesV5.StickerSearch.Execute(args);
            Log.Debug("StickerSearch completed on {ServerName} for CallId: {CallId}. Result count: {ResultCount}",
                pinnedToServerName, searcher.CallId, response?.Results?.Count ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "StickerSearch failed on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
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
