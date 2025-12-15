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

namespace Dapr.E2E.Test.Workflow.App.ShardMapReduce.Activities;

/// <summary>
/// Synthetic worker activity: deterministic "work" + optional small delay.
/// No external dependencies.
/// </summary>
public sealed class MapWorkerActivity : WorkflowActivity<MapWorkerInput, long>
{
    public override async Task<long> RunAsync(WorkflowActivityContext context, MapWorkerInput input)
    {
        // Small delay to simulate work and create scheduling pressure.
        // Deterministic jitter derived from inputs (no randomness).
        var jitter = 0;
        if (input.DelayMsJitter > 0)
        {
            var h = MixToInt(input.Seed, input.ShardId, input.WorkerId);
            jitter = Math.Abs(h % (input.DelayMsJitter + 1));
        }

        var delay = Math.Max(0, input.DelayMsBase) + jitter;
        if (delay > 0)
        {
            await Task.Delay(delay);
        }

        // Deterministic "compute" result that is stable and easy to validate.
        return MixToLong(input.Seed, input.ShardId, input.WorkerId);
    }
    
    private static int MixToInt(int seed, int shardId, int workerId)
    {
        unchecked
        {
            var x = seed;
            x = (x * 397) ^ shardId;
            x = (x * 397) ^ shardId;
            x ^= (x << 13);
            x ^= (x >> 17);
            x ^= (x << 5);
            return x;
        }
    }
    
    private static long MixToLong(int seed, int shardId, int workerId)
    {
        unchecked
        {
            // Expand to 64-bit with a simple reversible-ish mix.
            var a = (uint)MixToInt(seed, shardId, workerId);
            var b = (uint)MixToInt(seed ^ 0x5bd1e995, shardId * 31 + 7, workerId * 17 + 3);
            return ((long)a << 32) | b;
        }
    }
}

public sealed record MapWorkerInput(
    int ShardId,
    int WorkerId,
    int Seed,
    int DelayMsBase,
    int DelayMsJitter);

