# MrSix Results Comparator

A powerful tool to compare search results between Control and Test MrSix environments with both CLI and modern desktop UI options.

## 🎯 Project Structure

The solution consists of three projects:

### 1. **MrSixResultsComparator.Core** (Class Library)
Shared business logic used by both console and desktop applications:
- **Configuration**: Application settings and configuration
- **Models**: Data models (SearchParameter, ComparisonResult, etc.)
- **Services**: Core business services
  - `ComparisonService`: Main comparison logic with event notifications
  - `StackSearchService`: Execute StackSearch operations
  - `SearchParameterService`: Database access for search parameters
  - `ShardValidationService`: Shard validation logic
  - `MrSixContextService`: HTTP communication with MrSix servers
  - `LoggingService`: Serilog configuration and management

### 2. **MrSixResultsComparator** (Console Application)
Traditional console application with Spectre.Console for beautiful CLI output:
- Real-time progress display
- Color-coded difference highlighting
- Summary tables grouped by SiteCode
- Detailed Serilog file logging for troubleshooting

**Best for**: Quick comparisons, CI/CD integration, scripting

### 3. **MrSixResultsComparator.BlazorApp** (WPF + Blazor Hybrid Desktop App)
Modern desktop application with web-based UI:
- **Real-time progress tracking** with visual progress bar and loading indicators
- **Persistent results** that remain visible between runs
- **Advanced filtering** by SiteCode, SearcherUserId, or match status (defaults to mismatches)
- **Two grouping modes**: Group by Search Service or by Site Code
- **Interactive result tables** showing detailed differences
- **Automatic retry system**: Verifies all mismatches are repeatable
- **Smart retry tracking**: Distinguishes transient vs confirmed mismatches
- **Parameter caching**: Loads parameters once, reuses for faster subsequent runs
- **Parameters viewer**: Dedicated page to view and filter cached parameters
- **Service enable/disable**: Selectively test specific search services via UI
- **Modern, responsive UI** with professional styling and desktop-optimized layout
- **Configuration panel** to modify settings without editing config files

**Best for**: Regular usage, detailed analysis, result comparison history, verifying real issues

## 🚀 Getting Started

### Prerequisites
- .NET 10.0 SDK
- SQL Server access to SearchData database
- Network access to MrSix servers

### Running the Console Application

```bash
cd MrSixResultsComparator
dotnet run
```

### Running the Blazor Desktop Application

```bash
cd MrSixResultsComparator.BlazorApp
dotnet run
```

Or build and run the executable:

```bash
dotnet build -c Release
.\bin\Release\net10.0-windows\MrSixResultsComparator.BlazorApp.exe
```

## ⚙️ Configuration

Configuration is managed in `AppConfiguration.cs`:

```csharp
public string MrSixControl { get; set; } = "DA1MASC805";  // Control server
public string MrSixTest { get; set; } = "DA1MASC804";     // Test server
public int MaxParallelism { get; set; } = 5;              // Parallel execution limit
public string SearchDataConnectionString { get; set; } = "...";
public HashSet<string> EnabledSearchServices { get; set; } = new HashSet<string>
{
    "Stack", "SearchV4.OnePush", "SearchV4.TwoWay", // ... all 12 services
};
```

In the **Blazor Desktop App**, you can modify these settings directly in the UI configuration panel.

### 🔧 Enabling/Disabling Search Services

You can selectively enable or disable any of the 12 search services:

**In Blazor App UI:**
- Use the interactive **Search Services** panel with checkboxes
- Enable All / Disable All buttons for quick configuration
- Visual indicators show enabled (green) vs disabled (gray) services

**In Console App:**
```csharp
var config = new AppConfiguration();

// Disable specific services
config.EnabledSearchServices.Remove("SearchHighlight.LitBatch");

// Or enable only specific services
config.EnabledSearchServices.Clear();
config.EnabledSearchServices.Add("Stack");
config.EnabledSearchServices.Add("SearchV4.OnePush");
```

See [SEARCH_SERVICES_CONFIGURATION.md](SEARCH_SERVICES_CONFIGURATION.md) for detailed documentation.

## 📊 Features

### Console App Features
- ✅ Spectre.Console beautiful CLI output
- ✅ Real-time progress during comparison
- ✅ Color-coded match/difference display
- ✅ Detailed summary tables by SiteCode
- ✅ Comprehensive JSON log files
- ✅ Suitable for automation and scripting
- ✅ All 12 search services supported

### Blazor Desktop App Features  
(See full list above - includes automatic retry, caching, parameter viewer, and more)

### Blazor Desktop App Features
- ✅ Modern web-based UI in desktop application
- ✅ Real-time progress bar with loading spinners and status messages
- ✅ Persistent results visible after completion
- ✅ **Automatic retry of mismatches** to verify repeatability
- ✅ **Smart mismatch classification** (transient vs confirmed)
- ✅ **Parameter caching** for instant subsequent runs
- ✅ **Parameters viewer page** to inspect cached data
- ✅ Filter by SiteCode, SearcherUserId, or match status (**defaults to mismatches**)
- ✅ **Two grouping modes**: by Search Service or by Site Code
- ✅ Detailed inline difference display with retry counts
- ✅ Summary cards showing key metrics (confirmed mismatches)
- ✅ Edit configuration without code changes
- ✅ **Enable/disable individual search services** via UI
- ✅ **Desktop-optimized layout** (1800px wide, side-by-side cards)
- ✅ Professional styling with gradients and shadows
- ✅ Responsive layout that adapts to window size

## 🔍 How It Works

1. **Validation**: Validates that Control and Test servers are on the same Shard
2. **Data Loading**: Retrieves search parameters from SearchData database (cached for reuse!)
3. **Parallel Execution**: Runs comparisons in parallel (configurable)
4. **Result Comparison**: Compares UserIds returned by each environment
5. **Auto-Retry**: Automatically re-runs all mismatches to verify repeatability
6. **Classification**: Marks mismatches as "transient" (matched on retry) or "confirmed" (repeatable)
7. **Reporting**: Displays/stores results with detailed differences and retry information

## 📈 Output Examples

### Console Output
```
✓ Match - SearcherUserId: 12345 (25 results)

═══ DIFF FOUND ═══
SearcherUserId: 67890
SiteCode: 100 | CallId: abc123...
CallTime: 2026-01-09 10:30:00

Control Result Count: 25
Test Result Count: 23

UserIds only in Control (2):
  11111, 22222

UserIds in both (23):
  33333, 44444, 55555...
```

### Blazor Desktop UI
- **Header**: Gradient header with app title and back navigation
- **Configuration Panel** (Left): Editable settings for servers and parallelism
- **Search Services Panel** (Right): Enable/disable services with checkboxes
- **Cache Status**: Shows cached parameter count with "View" link
- **Progress Section**: Real-time progress bar or loading spinner with detailed messages
- **Summary Cards**: Total, Matched (with transient count), Confirmed Mismatches, Success Rate
- **Results Tables**: Grouped by Service or SiteCode with expandable difference details
- **Filter Bar**: Search and filter results dynamically (defaults to mismatches)
- **Parameters Page** (`/parameters`): View and filter all cached search parameters

## 📝 Logging

Both applications use Serilog for detailed logging:
- Log files: `logs/stacksearch-comparison-YYYYMMDD-HHmmss.json`
- Structured JSON format for easy parsing
- Includes session information, server details, and all comparison results

## 🛠️ Development

### Building All Projects

```bash
dotnet build
```

### Running Tests (if added)

```bash
dotnet test
```

## 💡 Use Cases

**Use Console App When:**
- Running automated comparisons in CI/CD
- Quick one-off comparisons
- Integration with scripts or automation tools
- You prefer terminal-based workflows

**Use Blazor Desktop App When:**
- Regular daily usage
- Need to review results over time
- Want automatic retry verification of mismatches
- Need to distinguish transient vs real issues
- Want to adjust configuration frequently
- Want parameter caching for faster iterations
- Need to view and filter cached parameters
- Need to selectively enable/disable search services
- Prefer graphical interface
- Need to filter and analyze results interactively
- Presenting results to stakeholders
- Want to see results grouped by service or site

## 🔍 Troubleshooting & Diagnostics

### Shard 4 Reverse Search Issues

If you're experiencing inaccurate comparisons specifically with **Shard 4 Reverse searches**, we've created comprehensive diagnostic resources:

#### Quick Start
📋 **[QUICK_REFERENCE.md](QUICK_REFERENCE.md)** - Fast 5-minute guide to identify and fix the issue

#### Detailed Investigation
📊 **[INVESTIGATION_SUMMARY.md](INVESTIGATION_SUMMARY.md)** - Complete analysis of the root cause and fixes

#### Diagnostic Tools
🔧 **[SHARD4_DIAGNOSTIC.md](SHARD4_DIAGNOSTIC.md)** - Step-by-step troubleshooting guide  
💾 **[DiagnosticQueries.sql](DiagnosticQueries.sql)** - Database queries to investigate shard 4 issues

#### Key Issue
The `ReverseService` is missing the `OtherUserId` parameter in `ReverseArgs` constructor, which causes:
- Inconsistent results between Control and Test environments
- Non-deterministic user ID selection
- Higher mismatch rates on certain shards (particularly shard 4)

**Solution**: Pass `otherUserId: searcher.OtherUserId` to the `ReverseArgs` constructor in `ReverseService.cs`

### General Troubleshooting

**Comparison Fails:**
1. Check server accessibility (ping servers)
2. Verify SQL connection string in `AppConfiguration.cs`
3. Ensure database permissions for SearchData
4. Review logs in `logs/` folder

**No Parameters Loading:**
1. Verify connection string
2. Check ShardId validation
3. Ensure servers are on same shard
4. Run diagnostic queries from `DiagnosticQueries.sql`

**Mismatches on Specific Shards:**
1. Check `SHARD4_DIAGNOSTIC.md` for shard-specific issues
2. Review enhanced logging output for diagnostic messages
3. Compare server configurations
4. Run shard-specific diagnostic queries

## 🤝 Contributing

The code is well-structured for extensibility:
- Add new services to `Core/Services/`
- Extend models in `Core/Models/`
- Add UI components in `BlazorApp/Components/` or `BlazorApp/Pages/`
- Console helpers in `MrSixResultsComparator/Helpers/`

## 📄 License

Internal use only.

---

**Questions?** Contact the development team.
