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
}
