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
When the app launches, you'll see a configuration panel at the top:
- **Control Server**: The baseline MrSix server (e.g., `DA1MASC805`)
- **Test Server**: The server being tested (e.g., `DA1MASC804`)
- **Max Parallelism**: Number of parallel comparisons (1-20, default: 5)
- **Connection String**: SQL Server connection for SearchData database

### 2. Start Comparison
- Click the **"▶️ Start Comparison"** button
- The app will:
  1. Validate that both servers are on the same Shard
  2. Load search parameters from the database
  3. Execute comparisons in parallel
  4. Display results in real-time

### 3. View Progress
- Watch the progress bar fill up as comparisons complete
- Current status message shows what's happening
- Results appear as they complete

### 4. Review Results
After completion, you'll see:

#### Summary Cards
- **Total Comparisons**: Total number of searches compared
- **Matched**: Number of searches with identical results (green)
- **Mismatched**: Number of searches with differences (red)
- **Success Rate**: Percentage of matches

#### Results Tables (Grouped by SiteCode)
Each table shows:
- ✓ Match / ✗ Diff badge
- SearcherUserId
- Control Count vs Test Count
- Call ID (first 8 characters)
- Detailed differences for mismatches

#### Difference Details (for mismatches)
- **Only in Control**: UserIds only returned by Control server
- **Only in Test**: UserIds only returned by Test server
- **In Both**: UserIds returned by both servers

### 5. Filter Results
Use the filter bar to narrow down results:
- **Search box**: Type SiteCode or SearcherUserId to filter
- **Status dropdown**: Show All/Matched Only/Mismatched Only

### 6. Clear Results
Click **"🗑️ Clear Results"** to reset and start a new comparison

## 💡 Tips

### Adjusting Parallelism
- **Lower values (1-3)**: Slower but less load on servers
- **Medium values (4-7)**: Balanced performance (recommended)
- **Higher values (8-20)**: Faster but may overload servers

### Reading the Results
- **Green badges** = Perfect match between environments
- **Red badges** = Differences found, review details
- Look for patterns in mismatches (same SiteCode, similar SearcherUserIds)

### Troubleshooting
If comparison fails:
1. Check that servers are accessible (ping `DA1MASC805`, etc.)
2. Verify SQL Server connection string is correct
3. Ensure you have permissions to query SearchData database
4. Check logs folder for detailed error information: `logs/stacksearch-comparison-*.json`

## 📊 Understanding Results

### What is a "Match"?
A match means both Control and Test servers returned:
- Same number of results
- Identical UserIds in the results

### What is a "Mismatch"?
A mismatch occurs when:
- Different number of results returned
- Different UserIds in the results
- Same count but different UserIds

### Why Do Mismatches Happen?
Common reasons:
- Configuration differences between servers
- Data synchronization issues
- Algorithm changes being tested
- Timing issues (searches run at slightly different times)

## 🔍 Next Steps

After reviewing results:
1. **Investigate mismatches** - Check why specific searches differ
2. **Export results** - Take screenshots or copy data for reports
3. **Adjust configuration** - Change servers or parallelism if needed
4. **Run again** - Click Clear Results and start a new comparison

## 📝 Logs

Detailed logs are saved to:
```
C:\Work\_git\MrSixComparator\MrSixResultsComparator.BlazorApp\logs\
```

Log files include:
- Session ID and timestamps
- Server configuration details
- Every comparison result
- Error messages and stack traces

Files are in JSON format for easy parsing and analysis.

---

**Need Help?** Check the main README.md or contact the development team.
