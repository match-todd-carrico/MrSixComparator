namespace MrSixResultsComparator.Core.Models;

public class ComparisonResult
{
    public short SiteCode { get; set; }
    public int SearcherUserId { get; set; }
    public bool Matched { get; set; }
    public int ControlCount { get; set; }
    public int TestCount { get; set; }
    public List<int> OnlyInControl { get; set; } = new();
    public List<int> OnlyInTest { get; set; } = new();
    public List<int> InBoth { get; set; } = new();
    public Guid CallId { get; set; }
    public DateTime CallTime { get; set; }
}
