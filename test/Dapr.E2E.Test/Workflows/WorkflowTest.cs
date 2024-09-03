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
using Dapr.Workflow;

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
    }
}
