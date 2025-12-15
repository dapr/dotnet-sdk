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

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.E2E.Test.Workflow.App.ShardMapReduce;
using Dapr.E2E.Test.Workflow.App.ShardMapReduce.Activities;
using Dapr.E2E.Test.Workflow.App.SimpleWorkflow;
using Dapr.E2E.Test.Workflow.App.SimpleWorkflow.Activities;
using Dapr.Workflow;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Dapr.E2E.Test;

public partial class E2ETests : IAsyncLifetime
{
    [Fact]
    public async Task SimpleWorkflowShouldRunSuccessfully()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(1));

        using var app = Host.CreateDefaultBuilder([]).ConfigureServices(services =>
        {
            services.AddDaprWorkflow(opt =>
            {
                opt.RegisterWorkflow<SimpleWorkflow>();
                opt.RegisterActivity<AddNumbersActivity>();
            });
        }).Build();
        await app.StartAsync(cts.Token);

        var workflowClient = app.Services.GetRequiredService<DaprWorkflowClient>();
        const int operand1 = 10;
        const int operand2 = 20;
        const int expectedResult = operand1 + operand2;

        var instanceId = Guid.NewGuid().ToString(); 
        await workflowClient.ScheduleNewWorkflowAsync(nameof(SimpleWorkflow), instanceId, new SimpleWorkflowInput(operand1, operand2));

        var result = await workflowClient.WaitForWorkflowCompletionAsync(instanceId, true, cts.Token);
        Assert.Equal(expectedResult, result.ReadOutputAs<int>());
    }

    [Fact]
    public async Task TestMapReduceWorkflowAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        using var app = Host.CreateDefaultBuilder([]).ConfigureServices(services =>
        {
            services.AddDaprWorkflow(opt =>
            {
                opt.RegisterWorkflow<MapReduceWorkflow>();
                opt.RegisterWorkflow<ShardWorkflow>();
                opt.RegisterActivity<MapWorkerActivity>();
            });
        }).Build();
        await app.StartAsync(cts.Token);
        
        var workflowClient = app.Services.GetRequiredService<DaprWorkflowClient>();
        var input = new MapReduceInput(
            ShardCount: 200,
            WorkersPerShard: 10,
            WorkerDelayMsBase: 1,
            WorkerDelayMsJitter: 5,
            ShardBatchSize: 20,
            WorkerBatchSize: 50);

        var instanceId = Guid.NewGuid().ToString();
        await workflowClient.ScheduleNewWorkflowAsync(nameof(MapReduceWorkflow), instanceId, input);
        var result = await workflowClient.WaitForWorkflowCompletionAsync(instanceId, true, cts.Token);
        
        Assert.True(result.IsWorkflowCompleted);
        Assert.False(result.IsWorkflowRunning);
    }
}
