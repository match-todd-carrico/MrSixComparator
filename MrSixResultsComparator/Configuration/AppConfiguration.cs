namespace MrSixResultsComparator.Configuration;

public class AppConfiguration
{
    public string SearchDataConnectionString { get; set; } = 
        "Data Source=DA1MADB801;Initial Catalog=SearchData;Trusted_Connection=yes;App=MrSixHammer;Encrypt=False";
    
    public string MrSixControl { get; set; } = "DA1MASC805"; // Configure for environment A
    
    public string MrSixTest { get; set; } = "DA1MASC804"; // Configure for environment B
    
    public int MaxParallelism { get; set; } = 5;
    
    public Guid SessionGuid { get; set; } = Guid.NewGuid();
    
    public List<string> ExtensionParams { get; set; } = new List<string>();
}
