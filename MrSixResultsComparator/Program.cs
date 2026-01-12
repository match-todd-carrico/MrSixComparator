using MrSixResultsComparator.Core.Configuration;
using MrSixResultsComparator.Core.Services;
using MrSixResultsComparator.Helpers;

// Initialize Configuration
var config = new AppConfiguration();

// Optional: Configure which search services to enable/disable
// Example: Disable specific services
// config.EnabledSearchServices.Remove("SearchHighlight.LitBatch");
// config.EnabledSearchServices.Remove("SearchHighlight.LitSearch");
// Example: Enable only specific services
// config.EnabledSearchServices.Clear();
// config.EnabledSearchServices.Add("Stack");
// config.EnabledSearchServices.Add("SearchV4.OnePush");

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
var onePushService = new OnePushService(config);
var litBatchService = new LitBatchService(config);
var litSearchService = new LitSearchService(config);
var moreLikeThisService = new MoreLikeThisService(config);
var oneWayService = new OneWayService(config);
var expertPicksService = new ExpertPicksService(config);
var justForYouService = new JustForYouService(config);
var matchPicksService = new MatchPicksService(config);
var reverseService = new ReverseService(config);
var searchWowService = new SearchWowService(config);
var twoWayService = new TwoWayService(config);

var comparisonService = new ComparisonService(
    config, 
    stackSearchService, 
    onePushService,
    litBatchService,
    litSearchService,
    moreLikeThisService,
    oneWayService,
    expertPicksService,
    justForYouService,
    matchPicksService,
    reverseService,
    searchWowService,
    twoWayService);

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
