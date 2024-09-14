using System;
using System.Net.Http;
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

    private sealed record TestPayload(string Name, string Color);
}
