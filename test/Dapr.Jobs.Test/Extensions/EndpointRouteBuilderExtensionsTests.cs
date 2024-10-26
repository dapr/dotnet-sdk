// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

#nullable enable

using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.Models;
using Dapr.Jobs.Models.Responses;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.Jobs.Test.Extensions;

public class EndpointRouteBuilderExtensionsTest
{
    [Fact]
    public async Task MapDaprScheduledJobHandler_ValidRequest_ExecutesAction()
    {
        var server = CreateTestServer();
        var client = server.CreateClient();

        var serializedPayload = JsonSerializer.Serialize(new SamplePayload("Dapr", 789));
        var serializedPayloadBytes = Encoding.UTF8.GetBytes(serializedPayload);
        var jobDetails = new DaprJobDetails(new DaprJobSchedule("0 0 * * *"))
        {
            RepeatCount = 5,
            DueTime = DateTimeOffset.UtcNow,
            Ttl = DateTimeOffset.UtcNow.AddHours(1),
            Payload = serializedPayloadBytes
        };
        var content = new StringContent(JsonSerializer.Serialize(jobDetails), Encoding.UTF8, "application/json");

        const string jobName = "testJob";
        var response = await client.PostAsync($"/job/{jobName}", content);

        response.EnsureSuccessStatusCode();

        //Validate the job name and payload
        var validator = server.Services.GetRequiredService<Validator>();
        Assert.Equal(jobName, validator.JobName);
        Assert.Equal(serializedPayload, validator.SerializedPayload);
    }

    [Fact]
    public async Task MapDaprScheduledJobHandler_InvalidPayload()
    {
        // Arrange
        var server = CreateTestServer();
        var client = server.CreateClient();

        var content = new StringContent("", Encoding.UTF8, "application/json");

        // Act
        const string jobName = "testJob";
        var response = await client.PostAsync($"/job/{jobName}", content);

        var validator = server.Services.GetRequiredService<Validator>();
        Assert.Equal(jobName, validator.JobName);
        Assert.Null(validator.SerializedPayload);
    }

    private sealed record SamplePayload(string Name, int Count);

    public sealed class Validator
    {
        public string? JobName { get; set; }

        public string? SerializedPayload { get; set; }
    }

    private static TestServer CreateTestServer()
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<Validator>();
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapDaprScheduledJobHandler(async (string? jobName, DaprJobDetails? jobDetails, Validator validator, CancellationToken cancellationToken) =>
                    {
                        if (jobName is not null)
                            validator.JobName = jobName;
                        if (jobDetails?.Payload is not null)
                        {
                            var payloadString = Encoding.UTF8.GetString(jobDetails.Payload);
                            validator.SerializedPayload = payloadString;
                        }
                        await Task.CompletedTask;
                    });
                });
            });

        return new TestServer(builder);
    }
}
