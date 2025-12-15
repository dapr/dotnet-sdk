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

namespace WorkflowMapReduceDemo.Workflows.Activities;

/// <summary>
/// Synthetic worker activity: deterministic "work" + optional small delay.
/// No external dependencies.
/// </summary>
public sealed partial class MapWorkerActivity(ILogger<MapWorkerActivity> logger) : WorkflowActivity<MapWorkerInput, long>
{
    public override async Task<long> RunAsync(WorkflowActivityContext context, MapWorkerInput input)
    {
        // Small delay to simulate work and create scheduling pressure.
        // Deterministic jitter derived from inputs (no randomness).
        LogStart(input);
        var jitter = 0;
        if (input.DelayMsJitter > 0)
        {
            var range = (uint)input.DelayMsJitter + 1u;
            var hash = HashToUInt32(input.Seed, input.ShardId, input.WorkerId);
            jitter = (int)(hash % range);
        }

        var delay = Math.Max(0, input.DelayMsBase) + jitter;
        delay = Math.Min(delay, 5_000);

        if (delay > 0)
        {
            await Task.Delay(delay);
        }

        long result =
            (input.Seed & 0xFFFF) +
            (input.ShardId * 1_000L) +
            input.WorkerId;

        LogComplete(result);
        return result;
    }
    
    private static uint HashToUInt32(int seed, int shardId, int workerId)
    {
        unchecked
        {
            // Simple, deterministic, low-risk hash (no Math.Abs, no big multiplies).
            uint x = (uint)seed;
            x ^= (uint)shardId * 0x9E3779B9u;
            x ^= (uint)workerId * 0x85EBCA6Bu;

            // A couple of xorshift steps for mixing.
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            return x;
        }
    }
    
    [LoggerMessage(LogLevel.Information, "Activity starting: {Input}")]
    partial void LogStart(MapWorkerInput input);
    [LoggerMessage(LogLevel.Information, "Activity finished with result '{Result}'")]
    partial void LogComplete(long result);
}

public sealed record MapWorkerInput(
    int ShardId,
    int WorkerId,
    int Seed,
    int DelayMsBase,
    int DelayMsJitter);

