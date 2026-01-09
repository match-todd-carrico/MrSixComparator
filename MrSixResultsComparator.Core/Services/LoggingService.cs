using Serilog;
using Serilog.Formatting.Compact;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.Core.Services;

public class LoggingService
{
    public static void Initialize(AppConfiguration config)
    {
        var logFileName = $"logs/stacksearch-comparison-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            .WriteTo.File(new CompactJsonFormatter(), logFileName, rollingInterval: RollingInterval.Day)
            .Enrich.WithProperty("Application", "MrSixResultsComparator")
            .Enrich.WithProperty("SessionId", config.SessionGuid)
            .Enrich.WithProperty("ControlServer", config.MrSixControl)
            .Enrich.WithProperty("TestServer", config.MrSixTest)
            .CreateLogger();
        
        Log.Information("Starting StackSearch comparison session");
        Log.Information("Control Server: {ControlServer}, Test Server: {TestServer}", 
            config.MrSixControl, config.MrSixTest);
        Log.Information("Log file will be saved to: {LogFileName}", logFileName);
    }
    
    public static async Task CloseAndFlush()
    {
        Log.Information("StackSearch comparison session completed");
        await Log.CloseAndFlushAsync();
    }
}
