// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.E2E.Test
{

    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using FluentAssertions;
    using System;
    using System.Collections.Generic;
    using Google.Protobuf;
    using Dapr.Client;

    [System.Obsolete]
    public partial class E2ETests
    {
        [Fact]
        public async Task TestWorkflows()
        {
            string instanceId = "WorkflowTestInstanceId";
            string instanceId2 = "EventRaiseId";
            string workflowComponent = "dapr";
            string workflowName = "PlaceOrder";
            object input = "paperclips";
            Dictionary<string, string> workflowOptions = new Dictionary<string, string>();
            workflowOptions.Add("task_queue", "testQueue");
            CancellationToken cts = new CancellationToken();

            using var daprClient = new DaprClientBuilder().UseGrpcEndpoint(this.GrpcEndpoint).UseHttpEndpoint(this.HttpEndpoint).Build();
            var health = await daprClient.CheckHealthAsync();
            health.Should().Be(true, "DaprClient is not healthy");

            Thread.Sleep(10000);

            // START WORKFLOW TEST
            var startResponse = await daprClient.StartWorkflowAsync(
                instanceId: instanceId, 
                workflowComponent: workflowComponent,
                workflowName: workflowName,
                input: input,
                workflowOptions: workflowOptions,
                cancellationToken: cts);

            startResponse.InstanceId.Should().Be("WorkflowTestInstanceId", $"Instance ID {startResponse.InstanceId} was not correct");

            // GET INFO TEST
            var getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent, cts);
            getResponse.instanceId.Should().Be("WorkflowTestInstanceId");
            getResponse.runtimeStatus.Should().Be("RUNNING", $"Instance ID {getResponse.runtimeStatus} was not correct");

            // PAUSE TEST:
            await daprClient.PauseWorkflowAsync(instanceId, workflowComponent, cts);
            getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent, cts);
            getResponse.runtimeStatus.Should().Be("SUSPENDED", $"Instance ID {getResponse.runtimeStatus} was not correct");

            // RESUME TEST:
            await daprClient.ResumeWorkflowAsync(instanceId, workflowComponent, cts);
            getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent, cts);
            getResponse.runtimeStatus.Should().Be("RUNNING", $"Instance ID {getResponse.runtimeStatus} was not correct");

            // RAISE EVENT TEST
            await daprClient.RaiseWorkflowEventAsync(instanceId, workflowComponent, "ChangePurchaseItem", "computers", cts);
            getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent, cts);

            // TERMINATE TEST:
            await daprClient.TerminateWorkflowAsync(instanceId, workflowComponent, cts);
            getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent, cts);
            getResponse.runtimeStatus.Should().Be("TERMINATED", $"Instance ID {getResponse.runtimeStatus} was not correct");

            // PURGE TEST
            await daprClient.PurgeWorkflowAsync(instanceId, workflowComponent, cts);

            try 
            {
                getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent, cts);
            }
            catch (DaprException ex)
            {
                ex.InnerException.Message.Should().Contain("No such instance exists", $"Instance {instanceId} was not correctly purged");
            }

            // Start another workflow for event raising purposes
            startResponse = await daprClient.StartWorkflowAsync(instanceId: instanceId2,
                workflowComponent: workflowComponent,
                workflowName: workflowName,
                input: input,
                workflowOptions: workflowOptions,
                cancellationToken: cts);

            // RAISE EVENT TEST
            await daprClient.RaiseWorkflowEventAsync(instanceId2, workflowComponent, "ChangePurchaseItem", "computers", cts);
            await Task.Delay(TimeSpan.FromSeconds(30));
            getResponse = await daprClient.GetWorkflowAsync(instanceId2, workflowComponent, cts);
            var outputString = getResponse.properties["dapr.workflow.output"];
            outputString.Should().Be("\"computers\"", $"Purchased item {outputString} was not correct");

        }

    }
}