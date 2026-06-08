using System.Net.Http;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.BlazorApp.Data;

public class ExplainService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AppConfiguration _config;

    public ExplainService(IHttpClientFactory httpClientFactory, AppConfiguration config)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
    }

    public async Task<string?> FetchExplainAsync(string serverName, Guid callId)
    {
        if (callId == Guid.Empty)
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            var url = $"http://{serverName}:8888/admin/getExplain?callid={callId}";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return $"[Failed to fetch explain: HTTP {(int)response.StatusCode}]";

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"[Failed to fetch explain: {ex.Message}]";
        }
    }

    public Task<string?> FetchControlExplainAsync(Guid callId) =>
        FetchExplainAsync(_config.MrSixControl, callId);

    public Task<string?> FetchTestExplainAsync(Guid callId) =>
        FetchExplainAsync(_config.MrSixTest, callId);

    /// <summary>
    /// Fetch a specific explain sub-file (pools, removal, scoring, pq, tri, error) for
    /// one server. Each sub-file is a focused slice of the classic .txt — pools.txt in
    /// particular is the smallest practical view that still surfaces filter/removal
    /// decisions and pool population, which together explain the bulk of mismatches.
    ///
    /// kind: one of "pools", "removal", "scoring", "pq", "tri", "error" (no leading dot).
    /// </summary>
    public async Task<string?> FetchExplainFileAsync(string serverName, Guid callId, string kind)
    {
        if (callId == Guid.Empty)
            return null;

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // Endpoint shape per ExplainController.GetExplainFile (and its /web/ alias):
            //   GET /admin/getExplainFile?filename={callId}.{kind}.txt
            var filename = $"{callId}.{kind}.txt";
            var url = $"http://{serverName}:8888/admin/getExplainFile?filename={filename}";
            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return $"[Failed to fetch {kind}.txt: HTTP {(int)response.StatusCode}]";

            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            return $"[Failed to fetch {kind}.txt: {ex.Message}]";
        }
    }

    public Task<string?> FetchControlPoolsExplainAsync(Guid callId) =>
        FetchExplainFileAsync(_config.MrSixControl, callId, "pools");

    public Task<string?> FetchTestPoolsExplainAsync(Guid callId) =>
        FetchExplainFileAsync(_config.MrSixTest, callId, "pools");
}
