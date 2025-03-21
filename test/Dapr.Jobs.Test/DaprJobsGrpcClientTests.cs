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

using System;
using System.Net.Http;
using Dapr.Client.Autogen.Grpc.v1;
using Dapr.Jobs.Models;
using Moq;
using Xunit;

namespace Dapr.Jobs.Test;

public sealed class DaprJobsGrpcClientTests
{

    [Fact]
    public void ScheduleJobAsync_RepeatsCannotBeLessThanZero()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await client.ScheduleJobAsync("MyJob", DaprJobSchedule.Daily, null, null, -5, null, default);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void ScheduleJobAsync_JobNameCannotBeNull()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await client.ScheduleJobAsync(null, DaprJobSchedule.Daily, null, null, -5, null, default);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void ScheduleJobAsync_JobNameCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.ScheduleJobAsync(string.Empty, DaprJobSchedule.Daily, null, null, -5, null, default);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void ScheduleJobAsync_ScheduleCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.ScheduleJobAsync("MyJob", new DaprJobSchedule(string.Empty), null, null, -5, null, default);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void ScheduleJobAsync_TtlCannotBeEarlierThanStartingFrom()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            var date = DateTime.UtcNow.AddDays(10);
            var earlierDate = date.AddDays(-2);
            await client.ScheduleJobAsync("MyJob", DaprJobSchedule.Daily, null, date, null, earlierDate, default);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void GetJobAsync_NameCannotBeNull()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await client.GetJobAsync(null, default);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void GetJobAsync_NameCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.GetJobAsync(string.Empty, default);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void DeleteJobAsync_NameCannotBeNull()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await client.DeleteJobAsync(null, default);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }

    [Fact]
    public void DeleteJobAsync_NameCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

#pragma warning disable CS0618 // Type or member is obsolete
        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.DeleteJobAsync(string.Empty, default);
        });
#pragma warning restore CS0618 // Type or member is obsolete
    }
    
    [Fact]
    public void ShouldDeserialize_EveryExpression()
    {
        const string scheduleText = "@every 1m";
        var response = new GetJobResponse { Job = new Job { Name = "test", Schedule = scheduleText } };
        var schedule = DaprJobSchedule.FromExpression(scheduleText);
        
        var jobDetails = DaprJobsGrpcClient.DeserializeJobResponse(response);
        Assert.Null(jobDetails.Payload);
        Assert.Equal(0, jobDetails.RepeatCount);
        Assert.Null(jobDetails.Ttl);
        Assert.Null(jobDetails.DueTime);
        Assert.Equal(jobDetails.Schedule.ExpressionValue, schedule.ExpressionValue);
    }

    private sealed record TestPayload(string Name, string Color);
}
