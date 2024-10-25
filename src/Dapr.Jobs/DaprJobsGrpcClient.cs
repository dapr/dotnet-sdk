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
using Dapr.Jobs.Models;
using Dapr.Jobs.Models.Responses;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Jobs;

/// <summary>
/// A client for interacting with the Dapr endpoints.
/// </summary>
internal sealed class DaprJobsGrpcClient : DaprJobsClient
{
    /// <summary>
    /// Present only for testing purposes.
    /// </summary>
    internal readonly HttpClient httpClient;
    
    /// <summary>
    /// Used to populate options headers with API token value.
    /// </summary>
    internal readonly KeyValuePair<string, string>? apiTokenHeader;

    private readonly Autogenerated.Dapr.DaprClient client;
    private readonly string userAgent = UserAgent().ToString();

    // property exposed for testing purposes
    internal Autogenerated.Dapr.DaprClient Client => client;
    
    internal DaprJobsGrpcClient(
        Autogenerated.Dapr.DaprClient innerClient,
        HttpClient httpClient,
        KeyValuePair<string, string>? apiTokenHeader)
    {
        this.client = innerClient;
        this.httpClient = httpClient;
        this.apiTokenHeader = apiTokenHeader;

        this.httpClient.DefaultRequestHeaders.UserAgent.Add(UserAgent());
    }

    /// <summary>
    /// Schedules a job with Dapr.
    /// </summary>
    /// <param name="jobName">The name of the job being scheduled.</param>
    /// <param name="schedule">The schedule defining when the job will be triggered.</param>
    /// <param name="payload">The main payload of the job.</param>
    /// <param name="startingFrom">The optional point-in-time from which the job schedule should start.</param>
    /// <param name="repeats">The optional number of times the job should be triggered.</param>
    /// <param name="ttl">Represents when the job should expire. If both this and DueTime are set, TTL needs to represent a later point in time.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [Obsolete("The API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
    public override async Task ScheduleJobAsync(string jobName, DaprJobSchedule schedule,
        ReadOnlyMemory<byte>? payload = null, DateTimeOffset? startingFrom = null, int? repeats = null,
        DateTimeOffset? ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobName, nameof(jobName));
        ArgumentNullException.ThrowIfNull(schedule, nameof(schedule));

        var job = new Autogenerated.Job { Name = jobName, Schedule = schedule.ExpressionValue };

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            //Ignore our own cancellation
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            // Ignore a remote cancellation due to our own cancellation
        }
        catch (Exception ex)
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
    public override async Task<DaprJobDetails> GetJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
            throw new ArgumentNullException(nameof(jobName));

        try
        {
            var envelope = new Autogenerated.GetJobRequest { Name = jobName };
            var callOptions = CreateCallOptions(headers: null, cancellationToken);
            var response = await client.GetJobAlpha1Async(envelope, callOptions);
            return new DaprJobDetails(new DaprJobSchedule(response.Job.Schedule))
            {
                DueTime = response.Job.DueTime is not null ? DateTime.Parse(response.Job.DueTime) : null,
                Ttl = response.Job.Ttl is not null ? DateTime.Parse(response.Job.Ttl) : null,
                RepeatCount = response.Job.Repeats == default ? null : (int?)response.Job.Repeats,
                Payload = response.Job.Data.ToByteArray()
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            //Ignore our own cancellation
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            // Ignore a remote cancellation due to our own cancellation
        }
        catch (Exception ex)
        {
            throw new DaprException(
                "Get job operation failed: the Dapr endpoint indicated a failure. See InnerException for details.", ex);
        }

        throw new DaprException("Get job operation failed: the Dapr endpoint did not return the expected value.");
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

        try
        {
            var envelope = new Autogenerated.DeleteJobRequest { Name = jobName };
            var callOptions = CreateCallOptions(headers: null, cancellationToken);
            await client.DeleteJobAlpha1Async(envelope, callOptions);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            //Ignore our own cancellation
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
        {
            // Ignore a remote cancellation due to our own cancellation
        }
        catch (Exception ex)
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
