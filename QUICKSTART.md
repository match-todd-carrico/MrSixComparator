# Quick Start Guide - Blazor Desktop App

## 🚀 Running the Blazor Desktop Application

### Option 1: Run from Visual Studio
1. Open `MrSixComparator.sln` in Visual Studio
2. Set `MrSixResultsComparator.BlazorApp` as the startup project (right-click → Set as Startup Project)
3. Press F5 or click the Run button

### Option 2: Run from Command Line
```bash
cd C:\Work\_git\MrSixComparator\MrSixResultsComparator.BlazorApp
dotnet run
```

### Option 3: Build and Run Executable
```bash
cd C:\Work\_git\MrSixComparator\MrSixResultsComparator.BlazorApp
dotnet build -c Release
.\bin\Release\net10.0-windows\MrSixResultsComparator.BlazorApp.exe
```

## 📋 Using the Application

### 1. Configure Settings
When the app launches, you'll see a side-by-side configuration panel:

**Configuration Card (Left)**:
- **Control Server**: The baseline MrSix server (e.g., `DA1MASC805`)
- **Test Server**: The server being tested (e.g., `DA1MASC804`)
- **Max Parallelism**: Number of parallel comparisons (1-20, default: 5)

**Search Services Card (Right)**:
- Enable/disable specific search services
- Use checkboxes to select which services to test
- Quick "Enable All" / "Disable All" buttons

### 2. Start Comparison
- Click the **"▶️ Start Comparison"** button
- The app will:
  1. Check if search parameters are cached (instant if cached!)
  2. If not cached: Validate servers and load parameters from database
  3. Execute comparisons in parallel
  4. **Automatically retry** any mismatches to verify they're repeatable
  5. Display results in real-time

### 3. Parameter Caching
**First Run**:
- Loads parameters from database (takes a few seconds)
- Status shows: "Loading search parameters from database..."
- Parameters are cached for future runs

**Subsequent Runs**:
- Uses cached parameters (nearly instant!)
- Status shows: "Using cached search parameters (450 loaded)..."
- Badge displays: "Cached: 450 parameters (ShardId: 123) [View →]"

**To Refresh**:
- Click **"🔄 Refresh Parameters"** to reload from database
- Use when database has new test data

**To View**:
- Click **"View →"** on the cache badge to see all cached parameters
- Filter and inspect parameters before running comparisons

### 4. View Progress
- **Initial phase**: Spinner shows "Loading search parameters from database..."
- **Comparison phase**: Progress bar fills up as comparisons complete
- **Retry phase**: "Retrying X mismatched comparisons to verify repeatability..."
- Current status message shows what's happening

### 5. Review Results

#### Summary Cards
- **Total Comparisons**: Total number of searches compared
- **Matched**: Number of matches including transient issues resolved on retry
  - Shows "(X transient)" if some mismatches matched on retry
- **Confirmed Mismatches**: Only mismatches verified on retry (real issues!)
- **Success Rate**: Percentage including transient as successes

#### Results Tables
Two view modes:
- **🔍 Search Service**: Group by service type, then by SiteCode
- **📍 Site Code**: Group by SiteCode (traditional view)

**Default Filter**: Shows **Mismatched Only** by default (what needs attention!)

Each table shows:
- Status badges:
  - ✓ Match (green) - Matched on first run
  - ⚠️ Matched on Retry (yellow) - Transient mismatch, matched on retry
  - ✗ Diff (red) - Mismatch, not yet retried
  - ✗ Diff 🔁 Confirmed (red) - Mismatch confirmed on retry
- SearcherUserId
- Control Count → Retry Count (if retried)
- Test Count → Retry Count (if retried)
- Call ID (first 8 characters)
- Search Service (in SiteCode view)
- Detailed differences for mismatches

#### Difference Details (for mismatches)
- **Only in Control**: UserIds only returned by Control server
- **Only in Test**: UserIds only returned by Test server
- **In Both**: UserIds returned by both servers

### 6. Filter Results
Use the filter bar to narrow down results:
- **Search box**: Type SiteCode or SearcherUserId to filter
- **Status dropdown**: 
  - **Mismatched Only** (default) - Focus on problems
  - All Results - See everything
  - Matched Only - Verify successes

### 7. View Parameters
Click **"View →"** on the cache badge to:
- See all cached search parameters
- Filter by service type or search text
- Inspect parameter details before running comparisons
- Verify correct data is loaded

### 8. Clear Results
Click **"🗑️ Clear Results"** to reset and start a new comparison

## 💡 Tips

### Parameter Caching
- **First comparison**: Takes longer (loads from database)
- **Subsequent comparisons**: Nearly instant (uses cache)
- **Refresh when**: Database has new parameters or servers changed
- **View before running**: Check what will be tested

### Service Selection
- **Enable all**: Test everything comprehensively
- **Disable some**: Focus on specific services or skip incomplete ones
- **Settings persist**: No need to re-select each run

### Automatic Retry
- **Always enabled**: Mismatches are automatically retried
- **Focus on confirmed**: Only count verified mismatches
- **Transient issues**: Shown separately (yellow badges)

### Adjusting Parallelism
- **Lower values (1-3)**: Slower but less load on servers
- **Medium values (4-7)**: Balanced performance (recommended)
- **Higher values (8-20)**: Faster but may overload servers

### Reading the Results
- **⚠️ Matched on Retry** = Transient issue, probably not a real problem
- **🔁 Confirmed** = Repeatable difference, needs investigation
- **Default filter** shows only mismatches (saves time!)
- Look for patterns in confirmed mismatches

### Navigation
- **Main page** (`/`): Run comparisons and view results
- **Parameters page** (`/parameters`): View cached parameters
- Use **"View →"** and back buttons to navigate

## 🔍 Understanding Results

### What is a "Match"?
A match means both Control and Test servers returned:
- Same number of results
- Identical UserIds in the results

### What is a "Confirmed Mismatch"?
A confirmed mismatch occurs when:
- First run: Different results between Control and Test
- **Retry**: Still different after re-running the same search
- These are the real issues that need investigation

### What is "Matched on Retry"?
A transient mismatch occurs when:
- First run: Different results
- **Retry**: Results now match
- Likely causes: timing, cache, network latency
- **Not counted** as a failure (shown in yellow)

### Why Do Mismatches Happen?
Common reasons:
- **Configuration differences** between servers
- **Data synchronization issues**
- **Algorithm changes** being tested
- **Transient issues** (timing, cache, network) - caught by retry!

## 📊 Workflow Example

1. **Launch app** → Configuration already set
2. **First run**: 
   - Click "Start Comparison"
   - Wait for parameters to load (3-5 seconds)
   - Watch comparisons execute
   - See retry phase for mismatches
3. **Review results** (auto-filtered to mismatches)
   - 2 confirmed mismatches (red 🔁)
   - 1 transient (yellow ⚠️)
   - Focus on the 2 confirmed issues
4. **Second run**:
   - Click "Start Comparison" again
   - Uses cached parameters (instant!)
   - Runs much faster
5. **View parameters**:
   - Click "View →" to inspect what was tested
   - Filter by specific service if needed

## 🔧 Advanced Features

### Cached Parameters Page
- View all loaded search parameters
- Filter by service type (ClassName)
- Search across multiple fields
- Verify data before running
- Check distribution across services

### Service Enable/Disable
- Selectively test specific services
- Disable incomplete or problematic services
- Enable All / Disable All for quick changes
- 2-column grid with scrolling for easy selection

### Group by Service or SiteCode
- **Service view**: See which services have issues
- **SiteCode view**: See which sites have issues
- Toggle with one click
- Both respect same filters

### Desktop-Optimized Layout
- Wide 1800px layout uses full screen
- Side-by-side configuration cards
- Efficient use of horizontal space
- Professional admin tool appearance

## 🔨 Troubleshooting

If comparison fails:
1. Check that servers are accessible (ping `DA1MASC805`, etc.)
2. Verify SQL Server connection string in `AppConfiguration.cs`
3. Ensure you have permissions to query SearchData database
4. Click "Refresh Parameters" to reload from database
5. Check logs folder: `logs/stacksearch-comparison-*.json`

If parameters won't load:
1. Verify connection string in config
2. Check ShardId validation
3. Ensure servers are on same shard
4. Review logs for database errors

## 📝 Logs

Detailed logs are saved to:
```
C:\Work\_git\MrSixComparator\MrSixResultsComparator.BlazorApp\logs\
```

Log files include:
- Session ID and timestamps
- Server configuration details
- Every comparison result (initial and retry)
- Retry statistics
- Error messages and stack traces

Files are in JSON format for easy parsing and analysis.

---

**Need Help?** Check the main README.md or SEARCH_SERVICES_CONFIGURATION.md for more details.
