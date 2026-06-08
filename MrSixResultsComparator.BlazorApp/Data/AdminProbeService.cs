using System.Net.Http;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.BlazorApp.Data;

/// <summary>
/// Hits the MrSix Match service's /admin endpoints on Control and Test sandboxes to
/// capture the in-memory cache state for users-of-interest in a comparison result.
///
/// Distinct from <see cref="MrSixAdminControlService"/>, which talks to an out-of-process
/// control plane (different port, X-Api-Key auth) for restart / shard ops. This service is
/// read-only and uses the same plain HTTP path (port 8888, no auth headers) that
/// <see cref="ExplainService"/> already uses.
///
/// Raw response bodies are returned as strings — the Analysis page is responsible for
/// rendering them. That insulates the comparator from MrSix schema changes between
/// releases: a new field shows up in the JSON without breaking us.
/// </summary>
public sealed class AdminProbeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppConfiguration _config;

    public AdminProbeService(IHttpClientFactory httpClientFactory, AppConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    // ─── Bulk endpoints (one HTTP roundtrip for many userIds) ────────────

    public Task<string?> FetchUserDetailsV2ForListAsync(string serverName, IEnumerable<int> userIds) =>
        FetchAsync(serverName, $"/admin/getUserDetailsV2ForList?userIds={string.Join(",", userIds)}");

    public Task<string?> FetchUserDetailsV3ForListAsync(string serverName, IEnumerable<int> userIds) =>
        FetchAsync(serverName, $"/admin/getUserDetailsV3ForList?userIds={string.Join(",", userIds)}");

    // ─── Per-user endpoints (no bulk variant on MrSix side) ──────────────

    public Task<string?> FetchInteractionsAsync(string serverName, int userId) =>
        FetchAsync(serverName, $"/admin/getInteractions?userId={userId}&ct=json&it=all");

    public Task<string?> FetchBlocksAsync(string serverName, int userId, string blockType) =>
        FetchAsync(serverName, $"/admin/getBlocks?userId={userId}&type={blockType}");

    // Single-user V2/V3 for the cases where the bulk endpoints aren't worth the parsing,
    // e.g. the Searcher card on its own.
    public Task<string?> FetchUserDetailsV2Async(string serverName, int userId) =>
        FetchAsync(serverName, $"/admin/getUserDetailsV2?userId={userId}");

    public Task<string?> FetchUserDetailsV3Async(string serverName, int userId) =>
        FetchAsync(serverName, $"/admin/getUserDetailsV3?userId={userId}");

    // ─── Plumbing ────────────────────────────────────────────────────────

    private async Task<string?> FetchAsync(string serverName, string pathAndQuery)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            var url = $"http://{serverName}:8888{pathAndQuery}";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return $"[error: HTTP {(int)response.StatusCode} for {pathAndQuery}]";

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"[error: {ex.Message} for {pathAndQuery}]";
        }
    }
}
