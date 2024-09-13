﻿// ------------------------------------------------------------------------
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

using System.Net.Http.Headers;
using System.Reflection;
using Dapr.Jobs.Models.Responses;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Jobs;

/// <summary>
/// A client for interacting with the Dapr endpoints.
/// </summary>
internal sealed class DaprJobsGrpcClient : DaprJobsClient
{
    internal readonly HttpClient httpClient;
    
    private readonly GrpcChannel channel;
    private readonly Autogenerated.Dapr.DaprClient client;
    internal readonly KeyValuePair<string, string>? apiTokenHeader;

    private readonly string userAgent = UserAgent().ToString();

    // property exposed for testing purposes
    internal Autogenerated.Dapr.DaprClient Client => client;
    
    internal DaprJobsGrpcClient(
        GrpcChannel channel,
        Autogenerated.Dapr.DaprClient innerClient,
        HttpClient httpClient,
        KeyValuePair<string, string>? apiTokenHeader)
    {
        this.channel = channel;
        this.client = innerClient;
        this.httpClient = httpClient;
        this.apiTokenHeader = apiTokenHeader;

        this.httpClient.DefaultRequestHeaders.UserAgent.Add(UserAgent());
    }

    /// <summary>
    /// Schedules a recurring job using a cron expression.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="cronExpression">The systemd Cron-like expression indicating when the job should be triggered.</param>
    /// <param name="startingFrom">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and DueTime are set, TTL needs to represent a later point in time.</param>
    /// <param name="payload">The main payload of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public override async Task ScheduleCronJobAsync(string jobName, string cronExpression,
        DateTimeOffset? startingFrom = null,
        int? repeats = null,
        DateTimeOffset? ttl = null, ReadOnlyMemory<byte>? payload = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentNullException(nameof(jobName));
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentNullException(nameof(cronExpression));
        
        var job = new Autogenerated.Job {  Name = jobName, Schedule = cronExpression };

        if (startingFrom is not null)
            job.DueTime = ((DateTimeOffset)startingFrom).ToString("O");

        if (repeats is not null)
        {
            if (repeats < 0)
                throw new ArgumentOutOfRangeException(nameof(repeats));

            job.Repeats = (uint)repeats;
        }

        if (payload is not null)
            job.Data = new Any { Value = ByteString.CopyFrom(payload.Value.Span), TypeUrl = "dapr.io/schedule/jobpayload" };

        if (ttl is not null)
        {
            if (ttl <= startingFrom)
                throw new ArgumentException(
                    $"When both {nameof(ttl)} and {nameof(startingFrom)} are specified, {nameof(ttl)} must represent a later point in time");

            job.Ttl = ((DateTimeOffset)ttl).ToString("O");
        }
        
        var envelope = new Autogenerated.ScheduleJobRequest { Job = job };

        var callOptions = CreateCallOptions(headers: null, cancellationToken);

        try
        {
            await client.ScheduleJobAlpha1Async(envelope, callOptions);
        }
        catch (RpcException ex)
        {
            throw new DaprException(
                "Schedule job operation failed: the Dapr endpoint indicated a failure. See InnerException for details.",
                ex);
        }
    }

    /// <summary>
    /// Schedules a recurring job with an optional future starting date.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="interval">The interval at which the job should be triggered.</param>
    /// <param name="startingFrom">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional maximum number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and StartingFrom are set, TTL needs to represent a later point in time.</param>
    /// <param name="payload">The main payload of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public override async Task ScheduleIntervalJobAsync(string jobName, TimeSpan interval,
        DateTimeOffset? startingFrom = null, int? repeats = null,
        DateTimeOffset? ttl = null, ReadOnlyMemory<byte>? payload = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentNullException(nameof(jobName));

        var job = new Autogenerated.Job { Name = jobName, Schedule = interval.ToDurationString() };

        if (startingFrom is not null)
            job.DueTime = ((DateTimeOffset)startingFrom).ToString("O");

        if (repeats is not null)
        {
            if (repeats < 0)
                throw new ArgumentOutOfRangeException(nameof(repeats));

            job.Repeats = (uint)repeats;
        }

        if (payload is not null)
            job.Data = job.Data = new Any { Value = ByteString.CopyFrom(payload.Value.Span), TypeUrl = "dapr.io/schedule/jobpayload" };

        if (ttl is not null)
        {
            if (ttl < startingFrom)
                throw new ArgumentException(
                    $"When both {nameof(ttl)} and {nameof(startingFrom)} are specified, the {nameof(ttl)} must represent a later point in time");

            job.Ttl = ((DateTimeOffset)ttl).ToString("O");
        }
        
        var envelope = new Autogenerated.ScheduleJobRequest { Job = job};

        var callOptions = CreateCallOptions(headers: null, cancellationToken);

        try
        {
            await client.ScheduleJobAlpha1Async(envelope, callOptions);
        }
        catch (RpcException ex)
        {
            throw new DaprException(
                "Schedule job operation failed: the Dapr endpoint indicated a failure. See InnerException for details.",
                ex);
        }
    }

    /// <summary>
    /// Schedules a one-time job.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="scheduledTime">The point in time when the job should be run.</param>
    /// <param name="payload">Stores the main payload of the job which is passed to the trigger function.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public override async Task ScheduleOneTimeJobAsync(string jobName, DateTimeOffset scheduledTime,
        ReadOnlyMemory<byte>? payload = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentNullException(nameof(jobName));

        var job = new Autogenerated.Job { Name = jobName, DueTime = scheduledTime.ToString("O") };
        
        if (payload is not null)
            job.Data = job.Data = new Any { Value = ByteString.CopyFrom(payload.Value.Span), TypeUrl = "dapr.io/schedule/jobpayload" };

        var envelope = new Autogenerated.ScheduleJobRequest { Job = job };

        var callOptions = CreateCallOptions(headers: null, cancellationToken);

        try
        {
            await client.ScheduleJobAlpha1Async(envelope, callOptions);
        }
        catch (RpcException ex)
        {
            throw new DaprException(
                "Schedule job operation failed: the Dapr endpoint indicated a failure. See InnerException for details.",
                ex);
        }
    }

    /// <summary>
    /// Retrieves the details of a registered job.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The details comprising the job.</returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public override async Task<JobDetails> GetJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentNullException(nameof(jobName));

        var envelope = new Autogenerated.GetJobRequest { Name = jobName };

        var callOptions = CreateCallOptions(headers: null, cancellationToken);
        Autogenerated.GetJobResponse response;

        try
        {
            response = await client.GetJobAlpha1Async(envelope, callOptions);
        }
        catch (RpcException ex)
        {
            throw new DaprException(
                "Get job operation failed: the Dapr endpoint indicated a failure. See InnerException for details.", ex);
        }
        
        return new JobDetails
        {
            DueTime = response.Job.DueTime is not null ? DateTime.Parse(response.Job.DueTime) : null,
            TTL = response.Job.Ttl is not null ? DateTime.Parse(response.Job.Ttl) : null,
            RepeatCount = response.Job.Repeats == default ? null : response.Job.Repeats,
            Payload = response.Job.Data.ToByteArray()
        };
    }

    /// <summary>
    /// Deletes the specified job.
    /// </summary>
    /// <param name="jobName">The name of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns></returns>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public override async Task DeleteJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentNullException(nameof(jobName));

        var envelope = new Autogenerated.DeleteJobRequest { Name = jobName };

        var callOptions = CreateCallOptions(headers: null, cancellationToken);

        try
        {
            await client.DeleteJobAlpha1Async(envelope, callOptions);
        }
        catch (RpcException ex)
        {
            throw new DaprException(
                "Delete job operation failed: the Dapr endpoint indicated a failure. See InnerException for details.",
                ex);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.channel.Dispose();
            this.httpClient.Dispose();
        }
    }

    private CallOptions CreateCallOptions(Metadata? headers, CancellationToken cancellationToken)
    {
        var callOptions = new CallOptions(headers: headers ?? new Metadata(), cancellationToken: cancellationToken);

        callOptions.Headers!.Add("User-Agent", this.userAgent);

        if (apiTokenHeader is not null)
            callOptions.Headers.Add(apiTokenHeader.Value.Key, apiTokenHeader.Value.Value);

        return callOptions;
    }

    /// <summary>
    /// Returns the value for the User-Agent.
    /// </summary>
    /// <returns>A <see cref="ProductInfoHeaderValue"/> containing the value to use for the User-Agent.</returns>
    private static ProductInfoHeaderValue UserAgent()
    {
        var assembly = typeof(DaprJobsClient).Assembly;
        var assemblyVersion = assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;

        return new ProductInfoHeaderValue("dapr-sdk-dotnet", $"v{assemblyVersion}");
    }
}
