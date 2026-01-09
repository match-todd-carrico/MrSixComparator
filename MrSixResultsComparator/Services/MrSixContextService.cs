using System.Text.Json;
using System.Text;
using MrSixResultsComparator.Models;

namespace MrSixResultsComparator.Services;

public class MrSixContextService
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public SearchIndexEngineStatus GetEngineStatus(string serverName)
    {
        var userInfoUrl = $"http://{serverName}:8888/admin/getEngineStatus";

        try
        {
            var url = new Uri(userInfoUrl);
            var status = TryGetStatus(url);

            if (status?.StatusBag == null) // second Try
                status = TryGetStatus(url);

            if (status?.StatusBag == null) // third Try
                status = TryGetStatus(url);

            return status ?? new SearchIndexEngineStatus();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetEngineStatus failed for {serverName}: [{ex.Message}]");
            return new SearchIndexEngineStatus();
        }
    }

    private static SearchIndexEngineStatus? TryGetStatus(Uri url)
    {
        var response = GetResponseFromUri(url);
        return JsonSerializer.Deserialize<SearchIndexEngineStatus>(response);
    }

    private static string GetResponseFromUri(Uri uri)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Add("Accept", "application/json; charset=utf-8");
            
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1000));
            var response = _httpClient.Send(request, cts.Token);
            response.EnsureSuccessStatusCode();
            
            using var responseStream = response.Content.ReadAsStream();
            using var reader = new StreamReader(responseStream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
        catch (TaskCanceledException)
        {
            return string.Empty;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
