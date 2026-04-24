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

    // Wall-clock bookends for the most recent run. Unlike summing per-result durations (which
    // captures per-worker CPU time across parallel searches), these measure end-to-end elapsed
    // time as the user experienced it.
    public DateTime? RunStartedUtc { get; private set; }
    public DateTime? RunCompletedUtc { get; private set; }

    public TimeSpan? RunDuration =>
        RunStartedUtc.HasValue
            ? (RunCompletedUtc ?? DateTime.UtcNow) - RunStartedUtc.Value
            : null;
    
    // Cached search parameters
    public List<SearchParameter>? CachedSearchParameters { get; set; }
    public int CachedShardId { get; set; }

    // ClassNames that were present in the cached parameters but did not run in the most recent
    // comparison, either because the service was toggled off or because the tool has no dispatch
    // entry for it. Populated by Index.razor after ComparisonService completes.
    public IReadOnlyList<SkippedClassNameInfo> SkippedClassNames { get; private set; } = Array.Empty<SkippedClassNameInfo>();

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
        if (isRunning && !IsRunning)
        {
            RunStartedUtc = DateTime.UtcNow;
            RunCompletedUtc = null;
        }
        else if (!isRunning && IsRunning)
        {
            RunCompletedUtc = DateTime.UtcNow;
        }

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
        RunStartedUtc = null;
        RunCompletedUtc = null;
        SkippedClassNames = Array.Empty<SkippedClassNameInfo>();
        NotifyStateChanged();
    }
    
    public void SetCachedSearchParameters(List<SearchParameter> parameters, int shardId)
    {
        CachedSearchParameters = parameters;
        CachedShardId = shardId;
        NotifyStateChanged();
    }

    public void SetSkippedClassNames(IReadOnlyList<SkippedClassNameInfo> skipped)
    {
        SkippedClassNames = skipped ?? Array.Empty<SkippedClassNameInfo>();
        NotifyStateChanged();
    }

    public ComparisonResult? GetResultById(Guid id)
    {
        lock (_lock)
        {
            return _results.FirstOrDefault(r => r.Id == id);
        }
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
