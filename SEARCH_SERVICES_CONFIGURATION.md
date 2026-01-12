# Search Services Configuration

## Overview
The MrSix Results Comparator now supports enabling/disabling individual search services for each comparison run. This allows you to selectively test specific search types without running all 12 services.

## Available Search Services

The following search services can be enabled or disabled:

1. **Stack** - Stack search using RecommendedArgs
2. **SearchV4.OnePush** - OnePush search
3. **SearchV4.TwoWay** - TwoWay search
4. **SearchV4.Reverse** - Reverse search
5. **SearchV4.MoreLikeThis** - MoreLikeThis search
6. **SearchV4.OneWay** - OneWay search
7. **SearchV4.SearchWow** - SearchWow search
8. **SearchHighlight.LitBatch** - LitBatch search
9. **SearchHighlight.LitSearch** - LitSearch search
10. **SearchV4.Recommended.ExpertPicks** - Expert Picks recommendations
11. **SearchV4.Recommended.JustForYou** - Just For You recommendations
12. **SearchV4.Recommended.MatchPicks** - Match Picks recommendations

## Configuration Methods

### Method 1: Blazor UI (Recommended)

The Blazor app provides an interactive UI to enable/disable search services:

1. Launch the Blazor app
2. Navigate to the **Search Services** section on the main page
3. Use checkboxes to enable/disable individual services
4. Use **Enable All** or **Disable All** buttons for quick configuration
5. The status bar shows how many services are currently enabled

**Features:**
- Visual indication of enabled (green) vs disabled (gray) services
- Real-time counter showing enabled services count
- Settings are disabled while a comparison is running
- Changes apply immediately to the next comparison run

### Method 2: Console App (Code Configuration)

In the console app (`MrSixResultsComparator/Program.cs`), you can programmatically configure enabled services:

#### Example 1: Disable Specific Services

```csharp
var config = new AppConfiguration();

// Disable services that are not yet fully implemented
config.EnabledSearchServices.Remove("SearchHighlight.LitBatch");
config.EnabledSearchServices.Remove("SearchHighlight.LitSearch");
config.EnabledSearchServices.Remove("SearchV4.SearchWow");
```

#### Example 2: Enable Only Specific Services

```csharp
var config = new AppConfiguration();

// Clear all and enable only specific services
config.EnabledSearchServices.Clear();
config.EnabledSearchServices.Add("Stack");
config.EnabledSearchServices.Add("SearchV4.OnePush");
config.EnabledSearchServices.Add("SearchV4.TwoWay");
```

#### Example 3: Test a Single Service

```csharp
var config = new AppConfiguration();

// Test only one service
config.EnabledSearchServices.Clear();
config.EnabledSearchServices.Add("Stack");
```

### Method 3: Direct Configuration Modification

You can also modify the default enabled services in `AppConfiguration.cs`:

```csharp
public HashSet<string> EnabledSearchServices { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
{
    "Stack",
    "SearchV4.OnePush",
    // Comment out or remove services you want disabled by default
    // "SearchHighlight.LitBatch",
    // "SearchHighlight.LitSearch",
};
```

## How It Works

### Filtering Logic

The `ComparisonService` automatically filters search parameters based on their `ClassName` property:

```csharp
var enabledSearchParameters = searchParameters
    .Where(sp => config.EnabledSearchServices.Contains(sp.ClassName))
    .ToList();
```

### Progress Reporting

The comparison progress includes information about filtered services:

- Total enabled comparisons to run
- Number of skipped searches (disabled services)
- Progress message: `"Starting comparison of {total} searches (skipped {skipped} disabled services)..."`

### Logging

When services are disabled, the following is logged:

```
Skipping {SkippedCount} search parameters with disabled service types
```

## Use Cases

### 1. Testing New Services
When implementing a new search service, disable all others to focus on testing:

```csharp
config.EnabledSearchServices.Clear();
config.EnabledSearchServices.Add("SearchV4.YourNewService");
```

### 2. Debugging Specific Issues
If one service is causing problems, isolate it:

```csharp
config.EnabledSearchServices.Clear();
config.EnabledSearchServices.Add("SearchV4.OnePush"); // Test only this one
```

### 3. Performance Testing
Test with different combinations to measure performance impact:

```csharp
// Test only fast services
config.EnabledSearchServices.Clear();
config.EnabledSearchServices.Add("Stack");
config.EnabledSearchServices.Add("SearchV4.OnePush");
config.EnabledSearchServices.Add("SearchV4.TwoWay");
```

### 4. Production Validation
Disable services that are not yet production-ready:

```csharp
// Remove experimental services
config.EnabledSearchServices.Remove("SearchV4.SearchWow");
config.EnabledSearchServices.Remove("SearchHighlight.LitBatch");
config.EnabledSearchServices.Remove("SearchHighlight.LitSearch");
```

## Implementation Details

### Configuration Class
**File:** `MrSixResultsComparator.Core/Configuration/AppConfiguration.cs`

- Property: `EnabledSearchServices` (HashSet<string>)
- Case-insensitive comparison
- All services enabled by default

### Comparison Service
**File:** `MrSixResultsComparator.Core/Services/ComparisonService.cs`

- Method: `CompareSearchResults()` - Filters parameters before execution
- Method: `GetAvailableSearchServices()` - Returns list of all registered services

### UI Components
**File:** `MrSixResultsComparator.BlazorApp/Pages/Index.razor`

- Service toggle checkboxes with visual indicators
- Enable All / Disable All buttons
- Enabled count display
- Disabled during comparison runs

### Styling
**File:** `MrSixResultsComparator.BlazorApp/wwwroot/css/app.css`

- `.services-grid` - Responsive grid layout
- `.service-toggle` - Individual service controls
- `.service-enabled` / `.service-disabled` - Color coding

## Best Practices

1. **Default Configuration**: Keep all services enabled in production configuration files
2. **Development**: Disable unimplemented services during development
3. **Testing**: Test services individually first, then in groups
4. **Documentation**: Update this file when adding new search services
5. **Logging**: Always check logs for skipped searches to verify configuration

## Troubleshooting

### No Comparisons Running
- Check that at least one service is enabled
- Verify search parameters exist for enabled services in the database

### Services Not Being Skipped
- Ensure the `ClassName` in `SearchParameter` matches exactly (case-insensitive)
- Check that services are properly registered in `ComparisonService` constructor

### UI Not Updating
- Refresh the page if checkboxes appear stuck
- Verify the app is not in a running state

## Future Enhancements

Potential improvements:
- Persist service configuration between app sessions
- Add configuration profiles (e.g., "All", "Production", "Testing")
- Export/import configuration settings
- Service-level statistics (average time, success rate)
- Group enable/disable by service category (SearchV4, SearchHighlight, Recommended)
