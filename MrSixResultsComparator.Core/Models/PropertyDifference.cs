namespace MrSixResultsComparator.Core.Models;

public class PropertyDifference
{
    public int UserId { get; set; }
    public string PropertyName { get; set; } = string.Empty;
    public string ControlValue { get; set; } = string.Empty;
    public string TestValue { get; set; } = string.Empty;
}
