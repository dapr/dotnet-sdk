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
            string instanceID = "testInstance";
            string workflowComponent = "testComponent";
            string workflowType = "PlaceOrder";
            object input = ByteString.CopyFrom(0x01);
            Dictionary<string, string> workflowOptions = new Dictionary<string, string>();
            workflowOptions.Add("task_queue", "testQueue");
            CancellationToken cts = new CancellationToken();

            using var daprClient = new DaprClientBuilder().UseGrpcEndpoint(this.GrpcEndpoint).UseHttpEndpoint(this.HttpEndpoint).Build();
            var health = await daprClient.CheckHealthAsync();
            health.Should().Be(true, "DaprClient is not healthy");

            // Start the workflow
            var startResponse = await daprClient.StartWorkflow(instanceID, workflowComponent, workflowType, input, workflowOptions, cts);
  
            // Get Request and validate
            startResponse.InstanceId.Should().Be("testInstance", "Instance ID was not correct");

            // GET INFO TEST
            var getResponse = await daprClient.GetWorkflow(instanceID, workflowComponent, workflowType);
            getResponse.InstanceId.Should().Be("testInstance");
            getResponse.Metadata["status"].Should().Be("Running", "The workflow is not running when it is expected to be running");


            // TERMINATE TEST:
            await daprClient.TerminateWorkflow(instanceID, workflowComponent);
            getResponse = await daprClient.GetWorkflow(instanceID, workflowComponent, workflowType);
            getResponse.Metadata["status"].Should().Be("Terminated", "The workflow is still running when it is expected to be terminated");

        }

    }
}