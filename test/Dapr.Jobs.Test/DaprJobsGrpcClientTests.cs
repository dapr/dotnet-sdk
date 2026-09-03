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
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Dapr.Client.Autogen.Grpc.v1;
using Dapr.Jobs.Models;
using Dapr.Jobs.Models.Responses;
using Dapr.Testcontainers.Xunit.Attributes;
using Moq;

namespace Dapr.Jobs.Test;

public sealed class DaprJobsGrpcClientTests
{
    [MinimumDaprRuntimeFact("1.18")]
    public void ScheduleJobAsync_RepeatsCannotBeLessThanZero()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

        var result = Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
        {
            await client.ScheduleJobAsync("MyJob", DaprJobSchedule.Daily, null, null, -5, null, default, cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void ScheduleJobAsync_JobNameCannotBeNull()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

        var result = Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await client.ScheduleJobAsync(null, DaprJobSchedule.Daily, null, null, -5, null, default, cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void ScheduleJobAsync_JobNameCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.ScheduleJobAsync(string.Empty, DaprJobSchedule.Daily, null, null, -5, null, default, cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void ScheduleJobAsync_ScheduleCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.ScheduleJobAsync("MyJob", new DaprJobSchedule(string.Empty), null, null, -5, null, default, cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void ScheduleJobAsync_TtlCannotBeEarlierThanStartingFrom()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            var date = DateTime.UtcNow.AddDays(10);
            var earlierDate = date.AddDays(-2);
            await client.ScheduleJobAsync("MyJob", DaprJobSchedule.Daily, null, date, null, earlierDate, default, cancellationToken: TestContext.Current.CancellationToken);
        });
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void GetJobAsync_NameCannotBeNull()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

        var result = Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await client.GetJobAsync(null, TestContext.Current.CancellationToken);
        });
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void GetJobAsync_NameCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.GetJobAsync(string.Empty, TestContext.Current.CancellationToken);
        });
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void DeleteJobAsync_NameCannotBeNull()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

        var result = Assert.ThrowsAsync<ArgumentNullException>(async () =>
        {
            await client.DeleteJobAsync(null, TestContext.Current.CancellationToken);
        });
    }

    [MinimumDaprRuntimeFact("1.18")]
    public void DeleteJobAsync_NameCannotBeEmpty()
    {
        var mockClient = Mock.Of<Client.Autogen.Grpc.v1.Dapr.DaprClient>();
        var httpClient = Mock.Of<HttpClient>();

        var client = new DaprJobsGrpcClient(mockClient, httpClient, null);

        var result = Assert.ThrowsAsync<ArgumentException>(async () =>
        {
            await client.DeleteJobAsync(string.Empty, TestContext.Current.CancellationToken);
        });
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

    [Fact]
    public void ShouldDeserialize_CronTzExpression_PersistsTimezone()
    {
        const string scheduleText = "CRON_TZ=Europe/Rome 0 */5 * * * *";
        var response = new GetJobResponse { Job = new Job { Name = "test", Schedule = scheduleText } };

        var jobDetails = DaprJobsGrpcClient.DeserializeJobResponse(response);

        Assert.Equal(scheduleText, jobDetails.Schedule.ExpressionValue);
        Assert.Equal("Europe/Rome", jobDetails.Schedule.TimeZone);
        Assert.True(jobDetails.Schedule.IsCronExpression);
    }

    [Fact]
    public void ShouldDeserialize_PopulatesJobName()
    {
        const string jobName = "my-scheduled-job";
        var response = new GetJobResponse { Job = new Job { Name = jobName, Schedule = "@daily" } };

        var jobDetails = DaprJobsGrpcClient.DeserializeJobResponse(response);

        Assert.Equal(jobName, jobDetails.Name);
    }

    [Fact]
    public void ShouldDeserialize_EmptyJobName_YieldsNullName()
    {
        var response = new GetJobResponse { Job = new Job { Name = string.Empty, Schedule = "@daily" } };

        var jobDetails = DaprJobsGrpcClient.DeserializeJobResponse(response);

        Assert.Null(jobDetails.Name);
    }

    [Fact]
    public void ShouldDeserialize_ListJobsResponse_MapsAllJobs()
    {
        var jobs = new List<Job>
        {
            new() { Name = "nightly-batch", Schedule = "@daily", Ttl = "2025-12-31T00:00:00Z" },
            new() { Name = "every-minute", Schedule = "@every 1m", Repeats = 5 },
            new() { Name = "one-shot", DueTime = "2025-08-15T12:00:00Z" }
        };

        var result = jobs.Select(DaprJobsGrpcClient.DeserializeJob).ToList();

        Assert.Equal(3, result.Count);
        Assert.Equal("nightly-batch", result[0].Name);
        Assert.Equal("@daily", result[0].Schedule.ExpressionValue);
        Assert.NotNull(result[0].Ttl);

        Assert.Equal("every-minute", result[1].Name);
        Assert.Equal("@every 1m", result[1].Schedule.ExpressionValue);
        Assert.Equal(5, result[1].RepeatCount);

        Assert.Equal("one-shot", result[2].Name);
        Assert.True(result[2].Schedule.IsPointInTimeExpression);
        Assert.NotNull(result[2].DueTime);
    }

    [Fact]
    public void ShouldDeserialize_EmptyJobList_YieldsEmptyCollection()
    {
        var response = new ListJobsResponse();

        var result = response.Jobs.Select(DaprJobsGrpcClient.DeserializeJob).ToList();

        Assert.Empty(result);
    }
}
