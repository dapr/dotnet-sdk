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
using WorkflowMapReduceDemo.Workflows.Activities;

namespace WorkflowMapReduceDemo.Workflows;

public sealed class ShardWorkflow : Workflow<ShardWorkflowInput, ShardWorkflowOutput>
{
    public override async Task<ShardWorkflowOutput> RunAsync(WorkflowContext context, ShardWorkflowInput input)
    {
        var workers = input.WorkersPerShard <= 0 ? 1 : input.WorkersPerShard;
        var maxParallelWorkers = input.WorkerBatchSize <= 0 ? workers : input.WorkerBatchSize;
        var workerIds = Enumerable.Range(0, workers);
        
        var workerOutputs = await context.ProcessInParallelAsync(
            workerIds,
            workerId =>
                context.CallActivityAsync<MapWorkerOutput>(
                    nameof(MapWorkerActivity),
                    new MapWorkerInput(
                        ShardId: input.ShardId,
                        WorkerId: workerId,
                        Seed: input.Seed,
                        DelayMsBase: input.WorkerDelayMsBase,
                        DelayMsJitter: input.WorkerDelayMsJitter)),
            maxParallelWorkers);
        
        var shardSum = workerOutputs.Sum(a => a.Result);
        var totalIntentionalDelayMs = workerOutputs.Sum(a => a.DelayMs);

        context.SetCustomStatus(new
        {
            Phase = "ShardCompleted",
            input.ShardId,
            Workers = workers,
            ShardSum = shardSum,
            IntentionalDelayMs = totalIntentionalDelayMs
        });

        return new ShardWorkflowOutput(shardSum, totalIntentionalDelayMs);
    }
}

public sealed record ShardWorkflowOutput(long Result, long ActivityDelayMs);
