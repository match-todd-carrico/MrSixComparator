using MrSIXProxyV2.Input;
using MrSIXProxyV2.ResultsV4;
using MrSIXProxyV2.SearchCriteria;
using Serilog;
using MrSixResultsComparator.Core.Models;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.Core.Services;

public class KeywordSearchService : ISearchService
{
    private readonly AppConfiguration _config;

    public KeywordSearchService(AppConfiguration config)
    {
        _config = config;
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteSearch(
        SearchParameter searcher,
        string pinnedToServerName,
        string? config = null,
        bool enableExplain = false)
    {
        return ExecuteKeywordSearch(searcher, pinnedToServerName, config, enableExplain);
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteKeywordSearch(
        SearchParameter searcher,
        string pinnedToServerName,
        string? config = null,
        bool enableExplain = false)
    {
        SearchResponse<SearchResultRow>? response = null;

        var args = new KeywordSearchArgs(
            platformId: 0,
            siteCode: searcher.SiteCode,
            shardId: searcher.ShardId,
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
            searchTerm: searcher.KeyWord ?? string.Empty)
        {
            PinnedToServername = pinnedToServerName
        };

        args.ExtensionParams = new List<string>(_config.ExtensionParams);

        if (enableExplain)
        {
            args.ExtensionParams.Add("explain");
            Log.Debug("Explain tracking enabled for KeywordSearch on {ServerName}", pinnedToServerName);
        }

        args.DynamicArgs ??= new Dictionary<string, string>();
        args.DynamicArgs["OCallId"] = searcher.CallId.ToString();
        args.TimeOutInSeconds = 10000;

        try
        {
            Log.Debug("Executing KeywordSearch on {ServerName} for CallId: {CallId}, Term: {Term}",
                pinnedToServerName, searcher.CallId, searcher.KeyWord);
            response = MrSIXProxyV2.SearchesV5.KeywordSearch.Execute(args);
            Log.Debug("KeywordSearch completed on {ServerName} for CallId: {CallId}. Result count: {ResultCount}",
                pinnedToServerName, searcher.CallId, response?.Results?.Count ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "KeywordSearch failed on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
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
