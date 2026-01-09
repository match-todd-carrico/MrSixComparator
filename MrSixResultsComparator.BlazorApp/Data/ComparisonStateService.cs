using MrSixResultsComparator.Core.Models;
using MrSixResultsComparator.Core.Services;

namespace MrSixResultsComparator.BlazorApp.Data;

public class ComparisonStateService
{
    private List<ComparisonResult> _results = new();
    
    public IReadOnlyList<ComparisonResult> Results => _results.AsReadOnly();
    public bool IsRunning { get; private set; }
    public int Current { get; private set; }
    public int Total { get; private set; }
    public string Message { get; private set; } = string.Empty;

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
        _results.Add(result);
        NotifyStateChanged();
    }

    public void ClearResults()
    {
        _results.Clear();
        Current = 0;
        Total = 0;
        Message = string.Empty;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();
}
