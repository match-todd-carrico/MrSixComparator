using Serilog;
using MrSixResultsComparator.Models;

namespace MrSixResultsComparator.Services;

public class ShardValidationService
{
    private readonly MrSixContextService _contextService;

    public ShardValidationService(MrSixContextService contextService)
    {
        _contextService = contextService;
    }

    public int ValidateAndGetShardId(string controlServer, string testServer)
    {
        var controlStatus = TryGetSearchIndexEngineStatus(controlServer);
        var testStatus = TryGetSearchIndexEngineStatus(testServer);

        // Safely extract and compare ShardIds
        int? controlShardId = ExtractShardId(controlStatus);
        int? testShardId = ExtractShardId(testStatus);

        if (controlShardId != testShardId)
        {
            Log.Information("Control and Test are not on the same Shard: Control: {ControlShard}; Test: {TestShard}", 
                controlShardId?.ToString() ?? "NULL", 
                testShardId?.ToString() ?? "NULL");
        }

        return controlShardId ?? throw new InvalidOperationException("Unable to retrieve ShardId from control server");
    }

    private SearchIndexEngineStatus? TryGetSearchIndexEngineStatus(string mrSixServer)
    {
        var controlStatus = _contextService.GetEngineStatus(mrSixServer);
        if (controlStatus?.StatusBag == null)
        {
            for (int i = 0; i < 5; i++)
            {
                controlStatus = _contextService.GetEngineStatus(mrSixServer);
                if (controlStatus?.StatusBag != null)
                    break;
            }
        }

        return controlStatus;
    }

    private int? ExtractShardId(SearchIndexEngineStatus? status)
    {
        if (status?.StatusBag != null && status.StatusBag.TryGetValue("ShardId", out var shardValue))
        {
            if (int.TryParse(shardValue?.ToString(), out int parsedShard))
            {
                return parsedShard;
            }
        }
        return null;
    }
}
