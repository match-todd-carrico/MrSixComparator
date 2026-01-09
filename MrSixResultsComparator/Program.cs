using MrSixResultsComparator.Configuration;
using MrSixResultsComparator.Services;

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
await comparisonService.CompareSearchResults(searchParameters);

// Clean up
await LoggingService.CloseAndFlush();
