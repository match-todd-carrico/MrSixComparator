using MrSIXProxyV2.ResultsV4;
using MrSixResultsComparator.Core.Models;

namespace MrSixResultsComparator.Core.Services;

public interface ISearchService
{
    Task<SearchResponse<SearchResultRow>> ExecuteSearch(
        SearchParameter searcher, 
        string pinnedToServerName, 
        string? config = null,
        bool enableExplain = false);
    
    List<int> ExtractUserIds(SearchResponse<SearchResultRow> response);
    
    Dictionary<string, List<int>> ExtractUserIdsBySlotType(SearchResponse<SearchResultRow> response);
}
