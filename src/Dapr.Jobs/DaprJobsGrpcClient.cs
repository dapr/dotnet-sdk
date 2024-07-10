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
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapr.Jobs.Models.Responses;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Jobs;

/// <summary>
/// A client for interacting with the Dapr endpoints.
/// </summary>
internal class DaprJobsGrpcClient : DaprJobsClient
{
    private readonly Uri httpEndpoint;
    private readonly HttpClient httpClient;

    private readonly JsonSerializerOptions jsonSerializerOptions;

    private readonly GrpcChannel channel;
    private readonly Autogenerated.Dapr.DaprClient client;
    private readonly KeyValuePair<string, string>? apiTokenHeader;
    private readonly DaprJobClientOptions options;

    // property exposed for testing purposes
    internal Autogenerated.Dapr.DaprClient Client => client;

    public override JsonSerializerOptions JsonSerializerOptions => jsonSerializerOptions;

    internal DaprJobsGrpcClient(
        GrpcChannel channel,
        Autogenerated.Dapr.DaprClient innerClient,
        HttpClient httpClient,
        Uri httpEndpoint,
        JsonSerializerOptions jsonSerializerOptions,
        KeyValuePair<string, string>? apiTokenHeader,
        DaprJobClientOptions options)
    {
        this.channel = channel;
        this.client = innerClient;
        this.httpClient = httpClient;
        this.httpEndpoint = httpEndpoint;
        this.jsonSerializerOptions = jsonSerializerOptions;
        this.apiTokenHeader = apiTokenHeader;
        this.options = options;

        this.httpClient.DefaultRequestHeaders.UserAgent.Add(UserAgent());
    }

    /// <summary>
    /// Schedules a recurring job using a cron expression.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="cronExpression">The systemd Cron-like expression indicating when the job should be triggered.</param>
    /// <param name="dueTime">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and DueTime are set, TTL needs to represent a later point in time.</param>
    /// <param name="payload">The main payload of the job.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public override async Task ScheduleJobAsync<T>(string jobName, string cronExpression, DateTime? dueTime, uint? repeats = null,
        DateTime? ttl = null, T? payload = default, CancellationToken cancellationToken = default) where T : default
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentNullException(nameof(jobName));
        if (string.IsNullOrWhiteSpace(cronExpression))
            throw new ArgumentNullException(nameof(cronExpression));

        var job = new Autogenerated.Job {  Name = jobName, Schedule = cronExpression };

        if (dueTime is not null)
            job.DueTime = ((DateTime)dueTime).ToString("O");

        if (repeats is not null)
            job.Repeats = (uint)repeats;

        if (payload is not null)
            job.Data = Any.Pack(payload);

        if (ttl is not null)
        {
            if (ttl <= dueTime)
                throw new ArgumentException(
                    $"When both {nameof(ttl)} and {nameof(dueTime)} are specified, {nameof(ttl)} must represent a later point in time");

            job.Ttl = ((DateTime)ttl).ToString("O");
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
    public override async Task ScheduleJobAsync<T>(string jobName, TimeSpan interval, DateTime? startingFrom, uint? repeats = null,
        DateTime? ttl = null, T? payload = default, CancellationToken cancellationToken = default) where T : default
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentNullException(nameof(jobName));

        var job = new Autogenerated.Job { Name = jobName, Schedule = interval.ToDurationString() };

        if (startingFrom is not null)
            job.DueTime = ((DateTime)startingFrom).ToString("O");

        if (repeats is not null)
            job.Repeats = (uint)repeats;

        if (payload is not null)
            job.Data = Any.Pack(payload);

        if (ttl is not null)
        {
            if (ttl < startingFrom)
                throw new ArgumentException(
                    $"When both {nameof(ttl)} and {nameof(startingFrom)} are specified, the {nameof(ttl)} must represent a later point in time");

            job.Ttl = ((DateTime)ttl).ToString("O");
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
    public override async Task ScheduleJobAsync<T>(string jobName, DateTime scheduledTime, T? payload = default,
        CancellationToken cancellationToken = default) where T : default
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentNullException(nameof(jobName));

        var job = new Autogenerated.Job { Name = jobName, DueTime = scheduledTime.ToString("O") };

        if (payload is not null)
            job.Data = Any.Pack(payload);
        
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
    public override async Task<JobDetails<T>> GetJobAsync<T>(string jobName, CancellationToken cancellationToken = default)
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

        var intervalRegex = new Regex("h|m|(ms)|s");

        return new JobDetails<T>
        {
            DueTime = response.Job.DueTime is not null ? DateTime.Parse(response.Job.DueTime) : null,
            TTL = response.Job.Ttl is not null ? DateTime.Parse(response.Job.Ttl) : null,
            Interval = response.Job.Schedule is not null && intervalRegex.IsMatch(response.Job.Schedule) ? response.Job.Schedule.FromDurationString() : null,
            CronExpression = response.Job.Schedule is null || !intervalRegex.IsMatch(response.Job.Schedule) ? response.Job.Schedule : null,
            RepeatCount = response.Job.Repeats == default ? null : response.Job.Repeats,
            Payload = response.Job.Data.Unpack<T>()
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

    private CallOptions CreateCallOptions(Metadata? headers, CancellationToken cancellationToken)
    {
        var callOptions = new CallOptions(headers: headers ?? new Metadata(), cancellationToken: cancellationToken);

        callOptions.Headers!.Add("User-Agent", UserAgent().ToString());

        if (apiTokenHeader is not null)
            callOptions.Headers.Add(apiTokenHeader.Value.Key, apiTokenHeader.Value.Value);

        return callOptions;
    }

    /// <summary>
    /// Returns the value for the User-Agent.
    /// </summary>
    /// <returns>A <see cref="ProductInfoHeaderValue"/> containing the value to use for the User-Agent.</returns>
    protected static ProductInfoHeaderValue UserAgent()
    {
        var assembly = typeof(DaprJobsClient).Assembly;
        var assemblyVersion = assembly
            .GetCustomAttributes<AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?
            .InformationalVersion;

        return new ProductInfoHeaderValue("dapr-sdk-dotnet", $"v{assemblyVersion}");
    }
}
