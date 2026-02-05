using MrSixResultsComparator.Core.Models;
using MrSixResultsComparator.Core.Services;

namespace MrSixResultsComparator.BlazorApp.Data;

public class ComparisonStateService
{
    private readonly List<ComparisonResult> _results = new();
    private readonly object _lock = new object();
    
    public IReadOnlyList<ComparisonResult> Results
    {
        get
        {
            lock (_lock)
            {
                return _results.ToList(); // Return a snapshot
            }
        }
    }
    
    public bool IsRunning { get; private set; }
    public int Current { get; private set; }
    public int Total { get; private set; }
    public string Message { get; private set; } = string.Empty;
    
    // Cached search parameters
    public List<SearchParameter>? CachedSearchParameters { get; set; }
    public int CachedShardId { get; set; }

    public event Action? StateChanged;

    public void SetProgress(int current, int total, string message)
    {
        Current = current;
        Total = total;
        Message = message;
        NotifyStateChanged();
    }

    public void SetRunning(bool isRunning)
    {
        IsRunning = isRunning;
        NotifyStateChanged();
    }

    public void AddResult(ComparisonResult result)
    {
        lock (_lock)
        {
            // Check if this result already exists (based on unique identifiers)
            var existingIndex = _results.FindIndex(r => 
                r.SiteCode == result.SiteCode && 
                r.SearcherUserId == result.SearcherUserId && 
                r.SearchServiceName == result.SearchServiceName);
            
            if (existingIndex >= 0)
            {
                // Update existing result (for retry scenarios)
                _results[existingIndex] = result;
            }
            else
            {
                // Add new result
                _results.Add(result);
            }
        }
        NotifyStateChanged();
    }

    public void ClearResults()
    {
        lock (_lock)
        {
            _results.Clear();
        }
        Current = 0;
        Total = 0;
        Message = string.Empty;
        NotifyStateChanged();
    }
    
    public void SetCachedSearchParameters(List<SearchParameter> parameters, int shardId)
    {
        CachedSearchParameters = parameters;
        CachedShardId = shardId;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
