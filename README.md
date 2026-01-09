# MrSix Results Comparator

A tool for comparing StackSearch results between two MrSix server environments (Control vs Test).

## Project Structure

```
MrSixResultsComparator/
├── Program.cs                          # Application entry point
├── Configuration/
│   └── AppConfiguration.cs            # Application configuration settings
├── Models/
│   ├── ComparisonResult.cs            # Model for comparison results
│   ├── SearchIndexEngineStatus.cs     # Model for engine status
│   └── SearchParameter.cs             # Model for search parameters
├── Services/
│   ├── ComparisonService.cs           # Orchestrates the comparison logic
│   ├── LoggingService.cs              # Manages Serilog configuration
│   ├── MrSixContextService.cs         # Communicates with MrSix servers
│   ├── SearchParameterService.cs      # Retrieves search parameters from database
│   ├── ShardValidationService.cs      # Validates shard configuration
│   └── StackSearchService.cs          # Executes StackSearch operations
└── Helpers/
    ├── OutputHelper.cs                # Console output formatting
    └── SummaryHelper.cs               # Summary report generation
```

## Architecture

### Configuration Layer
- **AppConfiguration**: Centralizes all configuration settings including connection strings, server names, and runtime parameters.

### Models Layer
- **SearchParameter**: Represents search criteria from the database
- **ComparisonResult**: Tracks whether a search matched between environments
- **SearchIndexEngineStatus**: Represents the status response from MrSix servers

### Services Layer
- **LoggingService**: Initializes and manages structured logging with Serilog
- **MrSixContextService**: HTTP client for MrSix server communication
- **ShardValidationService**: Validates that both servers are configured for the same shard
- **SearchParameterService**: Queries the database for search parameters to compare
- **StackSearchService**: Executes StackSearch operations and extracts results
- **ComparisonService**: Main orchestrator that coordinates the comparison workflow

### Helpers Layer
- **OutputHelper**: Formats console output for differences found
- **SummaryHelper**: Generates and displays summary reports by SiteCode

## Configuration

Edit `Configuration/AppConfiguration.cs` to configure:
- Database connection string
- Control server name
- Test server name
- Maximum parallelism level

## Workflow

1. **Initialize** - Load configuration and set up logging
2. **Validate** - Check that both servers are on the same shard
3. **Load Parameters** - Retrieve search parameters from database
4. **Compare** - Execute searches on both servers in parallel and compare results
5. **Report** - Display differences and generate summary report

## Output

- **Console**: Real-time progress and difference reports with color-coded output
- **Log Files**: Structured JSON logs in `logs/` directory with detailed comparison data

## Features

- Parallel execution for faster comparisons
- Detailed difference reporting showing:
  - UserIds only in Control
  - UserIds only in Test
  - UserIds in both
- Summary report grouped by SiteCode
- Comprehensive structured logging
- Retry logic for server status checks
