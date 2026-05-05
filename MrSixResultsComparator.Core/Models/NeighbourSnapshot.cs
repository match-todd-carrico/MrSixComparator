namespace MrSixResultsComparator.Core.Models;

public class NeighbourSnapshot
{
    public int AnchorUserId { get; set; }
    public string Server { get; set; } = string.Empty;  // "Control" or "Test"
    public int Position { get; set; }
    public int NeighbourUserId { get; set; }
    public bool IsAnchor { get; set; }  // true for the anchor user's own row
    public string FirstTie { get; set; } = string.Empty;
    public string SecondTie { get; set; } = string.Empty;
    public string ThirdTie { get; set; } = string.Empty;
    public string FourthTie { get; set; } = string.Empty;
    public string FifthTie { get; set; } = string.Empty;
    public string SixthTie { get; set; } = string.Empty;
}
