namespace MrSixResultsComparator.Core.Models;

public class SearchIndexEngineStatus
{
    public bool IsEngineReady { get; set; }
    public Dictionary<string, object>? StatusBag { get; set; }
}
