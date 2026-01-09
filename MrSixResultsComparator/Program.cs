using MrSixResultsComparator.Core.Configuration;
using MrSixResultsComparator.Core.Services;
using MrSixResultsComparator.Helpers;

// Initialize Configuration
var config = new AppConfiguration();

// Initialize Logging
LoggingService.Initialize(config);

// Validate and get ShardId
var contextService = new MrSixContextService();
var shardValidationService = new ShardValidationService(contextService);
int shardId = shardValidationService.ValidateAndGetShardId(config.MrSixControl, config.MrSixTest);

// Get search parameters from database
var searchParameterService = new SearchParameterService(config.SearchDataConnectionString);
var searchParameters = searchParameterService.GetSearchParameters(shardId);

// Execute comparisons
var stackSearchService = new StackSearchService(config);
var comparisonService = new ComparisonService(config, stackSearchService);

// Wire up console output handlers
comparisonService.ProgressUpdated += (sender, args) =>
{
    // Progress is already handled by Parallel.ForEachAsync internally
};

comparisonService.DifferenceFound += (sender, args) =>
{
    var result = args.Result;
    OutputHelper.DisplayDifference(result);
};

comparisonService.ComparisonCompleted += (sender, args) =>
{
    var result = args.Result;
    if (result.Matched)
    {
        OutputHelper.DisplayMatch(result);
    }
};

await comparisonService.CompareSearchResults(searchParameters);

// Display summary
SummaryHelper.DisplaySummary(comparisonService.Results.ToList());

// Clean up
await LoggingService.CloseAndFlush();
