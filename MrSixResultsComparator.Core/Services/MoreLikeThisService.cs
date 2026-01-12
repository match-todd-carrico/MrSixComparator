using MrSIXProxyV2.Input;
using MrSIXProxyV2.ResultsV4;
using MrSIXProxyV2.SearchCriteria;
using Serilog;
using MrSixResultsComparator.Core.Models;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.Core.Services;

public class MoreLikeThisService : ISearchService
{
    private readonly AppConfiguration _config;

    public MoreLikeThisService(AppConfiguration config)
    {
        _config = config;
    }

    public Task<SearchResponse<SearchResultRow>> ExecuteSearch(
        SearchParameter searcher, 
        string pinnedToServerName, 
        string? config = null)
    {
        SearchResponse<SearchResultRow>? response = null;

        // MoreLikeThisArgs requires: moreLikeThisUserId parameter (use searcher.OtherUserId)
        var args = new MoreLikeThisArgs(
            platformId: 0,
            siteCode: searcher.SiteCode,
            shardId: searcher.ShardId,
            sessionId: _config.SessionGuid,
            searcherUserId: searcher.SearcherUserId,
            maxRecordsToReturn: searcher.RequestCount,
            moreLikeThisUserId: searcher.OtherUserId,  // The user to find matches similar to
            geo: searcher.Geo)
        {
            PinnedToServername = pinnedToServerName
        };

        args.ExtensionParams = new List<string>(_config.ExtensionParams);

        args.DynamicArgs ??= new Dictionary<string, string>();
        args.DynamicArgs["OCallId"] = searcher.CallId.ToString();
        args.TimeOutInSeconds = 10000;

        try
        {
            Log.Debug("Executing MoreLikeThis on {ServerName} for CallId: {CallId}, OtherUserId: {OtherUserId}", 
                pinnedToServerName, searcher.CallId, searcher.OtherUserId);
            response = MrSIXProxyV2.SearchesV5.MoreLikeThis.Execute(args);
            Log.Debug("MoreLikeThis completed on {ServerName} for CallId: {CallId}. Result count: {ResultCount}", 
                pinnedToServerName, searcher.CallId, response?.Results?.Count ?? 0);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "MoreLikeThis failed on {ServerName} for CallId: {CallId}", pinnedToServerName, searcher.CallId);
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
