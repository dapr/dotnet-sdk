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

using Dapr.E2E.Test.Workflow.App.ShardMapReduce.Activities;
using Dapr.Workflow;

namespace Dapr.E2E.Test.Workflow.App.ShardMapReduce;

public sealed class ShardWorkflow : Workflow<ShardWorkflowInput, long>
{
    public override async Task<long> RunAsync(WorkflowContext context, ShardWorkflowInput input)
    {
        var workers = input.WorkersPerShard <= 0 ? 1 : input.WorkersPerShard;
        var maxParallelWorkers = input.WorkerBatchSize <= 0 ? workers : input.WorkerBatchSize;

        context.SetCustomStatus(new
        {
            Phase = "ShardStarting",
            input.ShardId,
            Workers = workers,
            MaxParallelWorkers = maxParallelWorkers,
        });

        var workerIds = Enumerable.Range(0, workers);

        context.SetCustomStatus(new
        {
            Phase = "RunningWorkers",
            input.ShardId,
            Workers = workers,
            MaxParallelWorkers = maxParallelWorkers,
        });

        // Fan-out workers using ParallelProcessAsync (limited parallelism).
        var workerOutputs = await context.ProcessInParallelAsync(
            workerIds,
            workerId =>
                context.CallActivityAsync<long>(
                    nameof(MapWorkerActivity),
                    new MapWorkerInput(
                        ShardId: input.ShardId,
                        WorkerId: workerId,
                        Seed: input.Seed,
                        DelayMsBase: input.WorkerDelayMsBase,
                        DelayMsJitter: input.WorkerDelayMsJitter)),
            maxParallelWorkers);

        long shardSum = workerOutputs.Sum();

        context.SetCustomStatus(new
        {
            Phase = "ShardCompleted",
            input.ShardId,
            Workers = workers,
            ShardSum = shardSum,
        });

        return shardSum;
    }
}
