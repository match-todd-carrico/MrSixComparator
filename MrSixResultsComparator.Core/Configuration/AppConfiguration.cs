namespace MrSixResultsComparator.Core.Configuration;

public class AppConfiguration
{
    public string SearchDataConnectionString { get; set; } = 
        "Data Source=DA1MADB801;Initial Catalog=SearchData;Trusted_Connection=yes;App=MrSixHammer;Encrypt=False";
    
    public string MrSixControl { get; set; } = "DA1MASC805"; // Configure for environment A
    
    public string MrSixTest { get; set; } = "DA1MASC804"; // Configure for environment B
    
    public int MaxParallelism { get; set; } = 5;
    
    public Guid SessionGuid { get; set; } = Guid.NewGuid();
    
    public List<string> ExtensionParams { get; set; } = new List<string>();
    
    // Configuration for which search services are enabled
    public HashSet<string> EnabledSearchServices { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Stack",
        "SearchV4.OnePush",
        "SearchHighlight.LitBatch",
        "SearchHighlight.LitSearch",
        "SearchV4.MoreLikeThis",
        "SearchV4.OneWay",
        "SearchV4.Recommended.ExpertPicks",
        "SearchV4.Recommended.JustForYou",
        "SearchV4.Recommended.MatchPicks",
        "SearchV4.Reverse",
        "SearchV4.SearchWow",
        "SearchV4.TwoWay"
    };
    
    // Auto-retry mismatched comparisons to verify repeatability
    public bool AutoRetryMismatches { get; set; } = true;
    
    // Ignore results where missing users have a LastLoginDate within the threshold
    // These are likely data movement artifacts from eventually consistent data model
    public bool IgnoreRecentLogins { get; set; } = true;
    
    // How many minutes back to consider a LastLoginDate as "recent" (default: 60 = 1 hour)
    public int RecentLoginThresholdMinutes { get; set; } = 60;
}
