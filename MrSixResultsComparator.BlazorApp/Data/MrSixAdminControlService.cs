using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.BlazorApp.Data;

public sealed class MrSixAdminControlService
{
    private readonly AppConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public MrSixAdminControlService(AppConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<ServerAdminState> GetServerState(ServerTarget target)
    {
        var server = GetServerConfig(target);
        var client = CreateClient(server.BaseUrl, server.ApiKey);

        try
        {
            var shardResponse = await client.GetAsync("/api/shardid");
            var serviceResponse = await client.GetAsync("/api/service/status");

            if (!shardResponse.IsSuccessStatusCode)
            {
                return ServerAdminState.Failed(server.Name, $"Shard request failed: {(int)shardResponse.StatusCode} {shardResponse.ReasonPhrase}");
            }

            if (!serviceResponse.IsSuccessStatusCode)
            {
                return ServerAdminState.Failed(server.Name, $"Service request failed: {(int)serviceResponse.StatusCode} {serviceResponse.ReasonPhrase}");
            }

            var shardContent = await shardResponse.Content.ReadAsStringAsync();
            var serviceContent = await serviceResponse.Content.ReadAsStringAsync();

            using var shardJson = JsonDocument.Parse(shardContent);
            using var serviceJson = JsonDocument.Parse(serviceContent);

            var shardValue = shardJson.RootElement.TryGetProperty("value", out var shardProp)
                ? shardProp.GetString()
                : null;
            var serviceStatus = serviceJson.RootElement.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString() ?? "Unknown"
                : "Unknown";
            var processRunning = serviceJson.RootElement.TryGetProperty("processRunning", out var processProp) &&
                                 processProp.ValueKind == JsonValueKind.True;
            var readiness = serviceJson.RootElement.TryGetProperty("readiness", out var readinessProp)
                ? readinessProp.GetString() ?? "Unknown"
                : "Unknown";
            bool? isReady = null;
            if (serviceJson.RootElement.TryGetProperty("isReady", out var isReadyProp) &&
                (isReadyProp.ValueKind == JsonValueKind.True || isReadyProp.ValueKind == JsonValueKind.False))
            {
                isReady = isReadyProp.GetBoolean();
            }

            return new ServerAdminState(server.Name, shardValue, serviceStatus, processRunning, readiness, isReady, null);
        }
        catch (Exception ex)
        {
            return ServerAdminState.Failed(server.Name, ex.Message);
        }
    }

    public async Task<OperationResult> SetShardId(ServerTarget target, int shardId)
    {
        var server = GetServerConfig(target);
        var client = CreateClient(server.BaseUrl, server.ApiKey);

        try
        {
            var response = await client.PutAsync($"/api/shardid/{shardId}", content: null);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return OperationResult.Fail($"{server.Name}: failed to set shard to {shardId}. {error}");
            }

            return OperationResult.Ok($"{server.Name}: shard updated to {shardId}.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"{server.Name}: {ex.Message}");
        }
    }

    public async Task<OperationResult> RestartService(ServerTarget target)
    {
        var server = GetServerConfig(target);
        var client = CreateClient(server.BaseUrl, server.ApiKey);

        try
        {
            var response = await client.PostAsync("/api/service/restart", content: null);
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                return OperationResult.Fail($"{server.Name}: failed to restart Match.MrSix. {error}");
            }

            return OperationResult.Ok($"{server.Name}: Match.MrSix restart requested.");
        }
        catch (Exception ex)
        {
            return OperationResult.Fail($"{server.Name}: {ex.Message}");
        }
    }

    private HttpClient CreateClient(string baseUrl, string apiKey)
    {
        var client = _httpClientFactory.CreateClient();
        var normalizedBase = baseUrl.Trim().TrimEnd('/');
        client.BaseAddress = new Uri(normalizedBase);
        client.DefaultRequestHeaders.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        return client;
    }

    private ServerConfig GetServerConfig(ServerTarget target)
    {
        return target switch
        {
            ServerTarget.Control => new ServerConfig(
                "Control",
                _config.ControlAdminApiBaseUrl,
                _config.ControlAdminApiKey),
            ServerTarget.Test => new ServerConfig(
                "Test",
                _config.TestAdminApiBaseUrl,
                _config.TestAdminApiKey),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };
    }

    private sealed record ServerConfig(string Name, string BaseUrl, string ApiKey);
}

public enum ServerTarget
{
    Control = 1,
    Test = 2
}

public sealed record ServerAdminState(
    string ServerName,
    string? ShardId,
    string ServiceStatus,
    bool ProcessRunning,
    string Readiness,
    bool? IsReady,
    string? Error)
{
    public static ServerAdminState Failed(string serverName, string error) =>
        new(serverName, null, "Unknown", false, "Unknown", null, error);
}

public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
