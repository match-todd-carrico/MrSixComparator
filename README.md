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
- **Real-time progress tracking** with visual progress bar
- **Persistent results** that remain visible between runs
- **Advanced filtering** by SiteCode, SearcherUserId, or match status
- **Interactive result tables** showing detailed differences
- **Modern, responsive UI** with professional styling
- **Configuration panel** to modify settings without editing config files

**Best for**: Regular usage, detailed analysis, result comparison history

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
```

In the **Blazor Desktop App**, you can modify these settings directly in the UI configuration panel.

## 📊 Features

### Console App Features
- ✅ Spectre.Console beautiful CLI output
- ✅ Real-time progress during comparison
- ✅ Color-coded match/difference display
- ✅ Detailed summary tables by SiteCode
- ✅ Comprehensive JSON log files
- ✅ Suitable for automation and scripting

### Blazor Desktop App Features
- ✅ Modern web-based UI in desktop application
- ✅ Real-time progress bar with status messages
- ✅ Persistent results visible after completion
- ✅ Filter by SiteCode, SearcherUserId, or match status
- ✅ Detailed inline difference display
- ✅ Summary cards showing key metrics
- ✅ Edit configuration without code changes
- ✅ Professional styling with gradients and shadows
- ✅ Responsive layout that adapts to window size

## 🔍 How It Works

1. **Validation**: Validates that Control and Test servers are on the same Shard
2. **Data Loading**: Retrieves search parameters from SearchData database
3. **Parallel Execution**: Runs comparisons in parallel (configurable)
4. **Result Comparison**: Compares UserIds returned by each environment
5. **Reporting**: Displays/stores results with detailed differences

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
- **Header**: Gradient header with app title
- **Configuration Panel**: Editable settings for servers and parallelism
- **Progress Section**: Real-time progress bar showing current/total
- **Summary Cards**: Total comparisons, matched, mismatched, success rate
- **Results Tables**: Grouped by SiteCode with expandable difference details
- **Filter Bar**: Search and filter results dynamically

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
- Want to adjust configuration frequently
- Prefer graphical interface
- Need to filter and analyze results interactively
- Presenting results to stakeholders

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
