namespace MrSixResultsComparator.Core.Configuration;

/// <summary>
/// Static mapping of SiteCode to ShardId, used to narrow the search-parameter load
/// to a specific subset of sites within a shard.
/// </summary>
public static class SiteCodeShardMap
{
    // SiteCode => ShardId.
    // Kept in ascending SiteCode order to keep the UI list stable.
    private static readonly IReadOnlyDictionary<short, int> _map = new Dictionary<short, int>
    {
        {   1, 0 },
        {   2, 0 },
        {   3, 0 },
        {   7, 0 },
        {   8, 0 },
        {  10, 0 },
        {  21, 0 },
        {  22, 0 },
        {  36, 2 },
        {  40, 3 },
        {  41, 4 },
        {  42, 4 },
        {  43, 4 },
        { 166, 0 },
        { 199, 1 },
        { 200, 1 },
        { 201, 1 },
        { 204, 1 },
        { 207, 0 },
    };

    public static IReadOnlyDictionary<short, int> Map => _map;

    /// <summary>
    /// Returns the SiteCodes known to live on the given ShardId, in ascending order.
    /// </summary>
    public static IReadOnlyList<short> GetSiteCodesForShard(int shardId) =>
        _map.Where(kvp => kvp.Value == shardId)
            .Select(kvp => kvp.Key)
            .OrderBy(s => s)
            .ToList();

    /// <summary>
    /// Returns the ShardId for the given SiteCode, or null if the SiteCode is not mapped.
    /// </summary>
    public static int? TryGetShardForSiteCode(short siteCode) =>
        _map.TryGetValue(siteCode, out var shard) ? shard : (int?)null;
}
