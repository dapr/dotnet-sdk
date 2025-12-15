// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using Dapr.Workflow;
using Microsoft.Extensions.Logging;

namespace WorkflowMapReduceDemo.Workflows;

public sealed partial class MapReduceWorkflow : Workflow<MapReduceInput, MapReduceResult> 
{
    public override async Task<MapReduceResult> RunAsync(WorkflowContext context, MapReduceInput input)
    {
        var logger = context.CreateReplaySafeLogger<MapReduceWorkflow>();
        
        input = input with
        {
            ShardCount = input.ShardCount <= 0 ? 1 : input.ShardCount,
            WorkersPerShard = input.WorkersPerShard <= 0 ? 1 : input.WorkersPerShard,
            ShardBatchSize = input.ShardBatchSize <= 0 ? input.ShardCount : input.ShardBatchSize
        };

        var status1 = new { Phase = "Starting", input.ShardCount, input.WorkersPerShard, input.ShardBatchSize };
        LogSettingStatus(logger, status1);
        context.SetCustomStatus(status1);

        var shardIds = Enumerable.Range(0, input.ShardCount);

        var status2 = new { Phase = "RunningShards", input.ShardCount, MaxParallelShards = input.ShardBatchSize };
        LogSettingStatus(logger, status2);
        context.SetCustomStatus(status2);

        var shardSums = await context.ProcessInParallelAsync(shardIds, shardId =>
        {
            var shardInput = new ShardWorkflowInput(shardId, input.WorkersPerShard, input.Seed, input.WorkerDelayMsBase,
                input.WorkerDelayMsJitter, input.WorkerBatchSize);

            return context.CallChildWorkflowAsync<long>(nameof(ShardWorkflow), shardInput);
        }, input.ShardBatchSize);

        long total = shardSums.Sum();

        var result = new MapReduceResult(total, input.ShardCount, input.WorkersPerShard);

        var status3 = new { Phase = "Completed", result.ShardCount, result.WorkersPerShard, result.Total };
        LogSettingStatus(logger, status3);
        context.SetCustomStatus(status3);

        return result;
    }
    
    [LoggerMessage(LogLevel.Information, "Setting custom status: {Status}")]
    static partial void LogSettingStatus(ILogger logger, object status);
}

public sealed record MapReduceInput(int ShardCount, int WorkersPerShard, int Seed = 12345, int WorkerDelayMsBase = 2, int WorkerDelayMsJitter = 5, int ShardBatchSize = 25, int WorkerBatchSize = 100);
public sealed record MapReduceResult(long Total, int ShardCount, int WorkersPerShard);
public sealed record ShardWorkflowInput(int ShardId, int WorkersPerShard, int Seed, int WorkerDelayMsBase, int WorkerDelayMsJitter, int WorkerBatchSize);
