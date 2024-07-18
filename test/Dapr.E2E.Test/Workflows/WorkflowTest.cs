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
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Client;
using FluentAssertions;
using Xunit;
using System.Linq;
using System.Diagnostics;
using Grpc.Net.Client;

namespace Dapr.E2E.Test
{
    [Obsolete]
    public partial class E2ETests
    {
        [Fact]
        public async Task TestWorkflowLogging()
        {
            // This test starts the daprclient and searches through the logfile to ensure the
            // workflow logger is correctly logging the registered workflow(s) and activity(s)

            Dictionary<string, bool> logStrings = new Dictionary<string, bool>();
            logStrings["PlaceOrder"] = false;
            logStrings["ShipProduct"] = false;
            var logFilePath = "../../../../../test/Dapr.E2E.Test.App/log.txt";
            var allLogsFound = false;
            var timeout = 30; // 30s
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
            using var daprClient = new DaprClientBuilder().UseGrpcEndpoint(this.GrpcEndpoint).UseHttpEndpoint(this.HttpEndpoint).Build();
            var health = await daprClient.CheckHealthAsync();
            health.Should().Be(true, "DaprClient is not healthy");

            var searchTask = Task.Run(async() =>
            {
                using (StreamReader reader = new StreamReader(logFilePath))
                {
                    string line;
                    while ((line = await reader.ReadLineAsync().WaitAsync(cts.Token)) != null)
                    {
                        foreach (var entry in logStrings)
                        {
                            if (line.Contains(entry.Key))
                            {
                                logStrings[entry.Key] = true;
                            }
                        }
                        allLogsFound = logStrings.All(k => k.Value);
                        if (allLogsFound)
                        {
                            break;
                        }
                    }
                }
            }, cts.Token);

            try
            {
                await searchTask;
            }
            finally
            {
                File.Delete(logFilePath);
            }
            if (!allLogsFound)
            {
                Assert.True(false, "The logs were not able to found within the timeout");
            }
        }
        [Fact]
        public async Task TestWorkflows()
        {
            const string instanceId = "testInstanceId";
            const string workflowComponent = "dapr";
            const string workflowName = "PlaceOrder";
            object input = "paperclips";
            var workflowOptions = new Dictionary<string, string> { { "task_queue", "testQueue" } };

            using var daprClient = new DaprClientBuilder().UseGrpcEndpoint(this.GrpcEndpoint).UseHttpEndpoint(this.HttpEndpoint).Build();
            var health = await daprClient.CheckHealthAsync();
            health.Should().Be(true, "DaprClient is not healthy");

            // START WORKFLOW TEST
            var startResponse = await daprClient.StartWorkflowAsync(
                instanceId: instanceId, 
                workflowComponent: workflowComponent,
                workflowName: workflowName,
                input: input,
                workflowOptions: workflowOptions);

            startResponse.InstanceId.Should().Be("testInstanceId", $"Instance ID {startResponse.InstanceId} was not correct");

            // GET INFO TEST
            var getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent);
            getResponse.InstanceId.Should().Be("testInstanceId");
            getResponse.RuntimeStatus.Should().Be(WorkflowRuntimeStatus.Running, $"Instance ID {getResponse.RuntimeStatus} was not correct");

            // PAUSE TEST:
            await daprClient.PauseWorkflowAsync(instanceId, workflowComponent);
            getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent);
            getResponse.RuntimeStatus.Should().Be(WorkflowRuntimeStatus.Suspended, $"Instance ID {getResponse.RuntimeStatus} was not correct");

            // RESUME TEST:
            await daprClient.ResumeWorkflowAsync(instanceId, workflowComponent);
            getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent);
            getResponse.RuntimeStatus.Should().Be(WorkflowRuntimeStatus.Running, $"Instance ID {getResponse.RuntimeStatus} was not correct");

            // RAISE EVENT TEST
            await daprClient.RaiseWorkflowEventAsync(instanceId, workflowComponent, "ChangePurchaseItem", "computers");
            getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent);

            // TERMINATE TEST:
            await daprClient.TerminateWorkflowAsync(instanceId, workflowComponent);
            getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent);
            getResponse.RuntimeStatus.Should().Be(WorkflowRuntimeStatus.Terminated, $"Instance ID {getResponse.RuntimeStatus} was not correct");

            // PURGE TEST
            await daprClient.PurgeWorkflowAsync(instanceId, workflowComponent);

            try 
            {
                getResponse = await daprClient.GetWorkflowAsync(instanceId, workflowComponent);
                Assert.True(false, "The GetWorkflowAsync call should have failed since the instance was purged");
            }
            catch (DaprException ex)
            {
                ex.InnerException.Message.Should().Contain("no such instance exists", $"Instance {instanceId} was not correctly purged");
            }
            
        }
        [Fact]
        public async Task TestEventRaisingWorkflows()
        {
            const string instanceId = "EventRaiseId";
            const string workflowComponent = "dapr";
            const string workflowName = "PlaceOrder";
            object input = "paperclips";
            var workflowOptions = new Dictionary<string, string> { { "task_queue", "testQueue" } };
            
            using var daprClient = new DaprClientBuilder()
                .UseGrpcEndpoint(this.GrpcEndpoint)
                .UseHttpEndpoint(this.HttpEndpoint).Build();
            var health = await daprClient.CheckHealthAsync();
            
            health.Should().Be(true, "DaprClient is not healthy");
            
            var startResponse = await daprClient.StartWorkflowAsync(
                instanceId: instanceId,
                workflowComponent: workflowComponent,
                workflowName: workflowName,
                input: input,
                workflowOptions: workflowOptions);
        
            // PARALLEL RAISE EVENT TEST
            var event1 = daprClient.RaiseWorkflowEventAsync(instanceId, workflowComponent, "ChangePurchaseItem", "computers");
            var event2 = daprClient.RaiseWorkflowEventAsync(instanceId, workflowComponent, "ChangePurchaseItem", "computers");
            var event3 = daprClient.RaiseWorkflowEventAsync(instanceId, workflowComponent, "ChangePurchaseItem", "computers");
            var event4 = daprClient.RaiseWorkflowEventAsync(instanceId, workflowComponent, "ChangePurchaseItem", "computers");
            var event5 = daprClient.RaiseWorkflowEventAsync(instanceId, workflowComponent, "ChangePurchaseItem", "computers");
        
            var externalEvents = Task.WhenAll(event1, event2, event3, event4, event5);
            await Task.WhenAny(externalEvents, Task.Delay(TimeSpan.FromSeconds(30)));
            externalEvents.IsCompletedSuccessfully.Should().BeTrue($"Unsuccessful at raising events. Status of events: {externalEvents.IsCompletedSuccessfully}");
            
            // Wait up to 30 seconds for the workflow to complete and check the output
            using var cts = new CancellationTokenSource(delay: TimeSpan.FromSeconds(30));
            var getResponse = await daprClient.WaitForWorkflowCompletionAsync(instanceId, workflowComponent, cts.Token);
            var outputString = getResponse.Properties["dapr.workflow.output"];
            outputString.Should().Be("\"computers\"", $"Purchased item {outputString} was not correct");
            var deserializedOutput = getResponse.ReadOutputAs<string>();
            deserializedOutput.Should().Be("computers", $"Deserialized output '{deserializedOutput}' was not expected");
        }
        [Fact]
        public async Task TestLargeMessageWorkflow()
        {
            const string instanceId = "testLargeMessageId";
            const string workflowComponent = "dapr";
            const string workflowName = "StartLargeOrder";
            object input = "paperclips";
            var workflowOptions = new Dictionary<string, string> { { "task_queue", "testQueue" } };
            const int messageSize = 32 * 1024 * 1024; // 32Mb  
            const int payloadOverhead = 2000; //substract to allow for some overhead.
            var largeString = GetRandomAlphaNumericString(messageSize - payloadOverhead);
            
            var channelOptions = new GrpcChannelOptions
            {
                MaxReceiveMessageSize = messageSize, MaxSendMessageSize = messageSize
            };
            
            using var daprClient = new DaprClientBuilder()
                .UseGrpcEndpoint(this.GrpcEndpoint)
                .UseGrpcChannelOptions(channelOptions)
                .UseHttpEndpoint(this.HttpEndpoint)
                .Build();
            
            var health = await daprClient.CheckHealthAsync();
            health.Should().Be(true, "DaprClient is not healthy");
            
            var startResponse = await daprClient.StartWorkflowAsync(
                instanceId: instanceId,
                workflowComponent: workflowComponent,
                workflowName: workflowName,
                input: input,
                workflowOptions: workflowOptions);
            
            var event1 = daprClient.RaiseWorkflowEventAsync(instanceId, workflowComponent, "FinishLargeOrder", largeString);
            await event1;
            event1.IsCompletedSuccessfully.Should().BeTrue($"Cant send large message {event1.Exception}");
            
            // Wait up to 30 seconds for the workflow to complete and check the output
            using var cts = new CancellationTokenSource(delay: TimeSpan.FromSeconds(30));
            var getResponse = await daprClient.WaitForWorkflowCompletionAsync(instanceId, workflowComponent, cts.Token);
            var outputString = getResponse.Properties["dapr.workflow.output"];
            outputString.Should().Be("\"" + largeString + "\"", $"Purchased item {outputString} was not correct");
            var deserializedOutput = getResponse.ReadOutputAs<string>();
            deserializedOutput.Should().Be(largeString, $"Deserialized output '{deserializedOutput}' was not expected");
        }
        private static string GetRandomAlphaNumericString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var rand = new Random();
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[rand.Next(s.Length)]).ToArray());
        }
    }
}
