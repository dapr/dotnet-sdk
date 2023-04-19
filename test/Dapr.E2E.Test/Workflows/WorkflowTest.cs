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
            string instanceId = "TestWorkflowInstanceID";
            string workflowComponent = "dapr";
            string workflowName = "PlaceOrder";
            object input = ByteString.CopyFrom(0x01);
            Dictionary<string, string> workflowOptions = new Dictionary<string, string>();
            workflowOptions.Add("task_queue", "testQueue");
            CancellationToken cts = new CancellationToken();

            using var daprClient = new DaprClientBuilder().UseGrpcEndpoint(this.GrpcEndpoint).UseHttpEndpoint(this.HttpEndpoint).Build();
            var health = await daprClient.CheckHealthAsync();
            health.Should().Be(true, "DaprClient is not healthy");

            // START WORKFLOW TEST
            var startResponse = await daprClient.StartWorkflowAsync(instanceId, workflowComponent, workflowName, input, workflowOptions, cts);
            startResponse.InstanceId.Should().Be("TestWorkflowInstanceID", $"Instance ID {startResponse.InstanceId} was not correct");

            // GET INFO TEST
            var getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent, workflowName);
            getResponse.instanceId.Should().Be("TestWorkflowInstanceID");
            getResponse.metadata["dapr.workflow.runtime_status"].Should().Be("RUNNING", $"Instance ID {getResponse.metadata["dapr.workflow.runtime_status"]} was not correct");

            // TERMINATE TEST:
            await daprClient.TerminateWorkflowAsync(instanceId, workflowComponent);
            getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent, workflowName);
            getResponse.metadata["dapr.workflow.runtime_status"].Should().Be("TERMINATED", $"Instance ID {getResponse.metadata["dapr.workflow.runtime_status"]} was not correct");

        }

    }
}