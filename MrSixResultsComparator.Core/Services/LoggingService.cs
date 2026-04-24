using Serilog;
using Serilog.Formatting.Compact;
using MrSixResultsComparator.Core.Configuration;

namespace MrSixResultsComparator.Core.Services;

public class LoggingService
{
    /// <summary>
    /// Absolute path to the log file created by the most recent <see cref="Initialize"/> call.
    /// Null until Initialize runs. Exposed so the UI can link directly to the current session's log.
    /// </summary>
    public static string? CurrentLogFilePath { get; private set; }

    /// <summary>
    /// Absolute path to the directory that holds the JSON log files for all sessions.
    /// </summary>
    public static string LogDirectory => Path.Combine(AppContext.BaseDirectory, "logs");

    public static void Initialize(AppConfiguration config, InMemoryLogSink? memorySink = null)
    {
        Directory.CreateDirectory(LogDirectory);

        var logFileName = Path.Combine(LogDirectory,
            $"stacksearch-comparison-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        CurrentLogFilePath = logFileName;

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console()
            // Do NOT set rollingInterval here: each session already gets a unique timestamped
            // filename, and Serilog's RollingInterval.Day would mutate the on-disk name
            // (inserting the date before the extension), which breaks our "Open log file"
            // link because CurrentLogFilePath no longer matches the actual file.
            // flushToDiskInterval forces periodic FileStream.Flush(true) so entries reach disk
            // even if the process dies before CloseAndFlush runs.
            .WriteTo.File(
                new CompactJsonFormatter(),
                logFileName,
                flushToDiskInterval: TimeSpan.FromSeconds(1))
            .Enrich.WithProperty("Application", "MrSixResultsComparator")
            .Enrich.WithProperty("SessionId", config.SessionGuid)
            .Enrich.WithProperty("ControlServer", config.MrSixControl)
            .Enrich.WithProperty("TestServer", config.MrSixTest);

        // The in-memory sink is optional for backward compatibility with tests/CLI entry points
        // that don't participate in DI.
        if (memorySink != null)
        {
            loggerConfig = loggerConfig.WriteTo.Sink(memorySink);
        }

        Log.Logger = loggerConfig.CreateLogger();
        
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
