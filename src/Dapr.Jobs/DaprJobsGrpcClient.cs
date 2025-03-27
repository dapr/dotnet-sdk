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

using System.Diagnostics.CodeAnalysis;
using Dapr.Common;
using Dapr.Jobs.Models;
using Dapr.Jobs.Models.Responses;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using static Dapr.Common.DaprExperimentalConstants;
using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Jobs;

/// <summary>
/// A client for interacting with the Dapr endpoints.
/// </summary>
internal sealed class DaprJobsGrpcClient : DaprJobsClient
{
    /// <summary>
    /// The HTTP client used by the client for calling the Dapr runtime.
    /// </summary>
    /// <remarks>
    /// Property exposed for testing purposes.
    /// </remarks>
    internal readonly HttpClient HttpClient;
    /// <summary>
    /// The Dapr API token value.
    /// </summary>
    /// <remarks>
    /// Property exposed for testing purposes.
    /// </remarks>
    internal readonly string? DaprApiToken;
    /// <summary>
    /// The autogenerated Dapr client.
    /// </summary>
    /// <remarks>
    /// Property exposed for testing purposes.
    /// </remarks>
    internal Autogenerated.Dapr.DaprClient Client { get; }
   
    internal DaprJobsGrpcClient(
        Autogenerated.Dapr.DaprClient innerClient,
        HttpClient httpClient,
        string? daprApiToken)
    {
        this.Client = innerClient;
        this.HttpClient = httpClient;
        this.DaprApiToken = daprApiToken;
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
    [Experimental(JobsIdentifier)]
    public override async Task ScheduleJobAsync(string jobName, DaprJobSchedule schedule,
        ReadOnlyMemory<byte>? payload = null, DateTimeOffset? startingFrom = null, int? repeats = null,
        DateTimeOffset? ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(jobName, nameof(jobName));
        ArgumentNullException.ThrowIfNull(schedule, nameof(schedule));

        var job = new Autogenerated.Job { Name = jobName };

        //Set up the schedule (recurring or point in time)
        if (schedule.IsPointInTimeExpression)
        {
            job.DueTime = schedule.ExpressionValue;
        }
        else if (schedule.IsCronExpression || schedule.IsPrefixedPeriodExpression || schedule.IsDurationExpression)
        {
            job.Schedule = schedule.ExpressionValue;
        }
        
        if (startingFrom is not null)
        {
            job.DueTime = ((DateTimeOffset)startingFrom).ToString("O");
        }
        
        if (repeats is not null)
        {
            if (repeats < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repeats));
            }

            job.Repeats = (uint)repeats;
        }

        if (payload is not null)
        {
            job.Data = new Any { Value = ByteString.CopyFrom(payload.Value.Span), TypeUrl = "dapr.io/schedule/jobpayload" };
        }

        if (ttl is not null)
        {
            if (ttl <= startingFrom)
            {
                throw new ArgumentException(
                    $"When both {nameof(ttl)} and {nameof(startingFrom)} are specified, {nameof(ttl)} must represent a later point in time");
            }

            job.Ttl = ((DateTimeOffset)ttl).ToString("O");
        }

        var envelope = new Autogenerated.ScheduleJobRequest { Job = job };

        var grpcCallOptions = DaprClientUtilities.ConfigureGrpcCallOptions(typeof(DaprJobsClient).Assembly, this.DaprApiToken, cancellationToken);

        try
        {
            await Client.ScheduleJobAlpha1Async(envelope, grpcCallOptions).ConfigureAwait(false);
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
    [Experimental(DaprExperimentalConstants.JobsIdentifier)]
    public override async Task<DaprJobDetails> GetJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new ArgumentNullException(nameof(jobName));
        }

        try
        {
            var envelope = new Autogenerated.GetJobRequest { Name = jobName };
            var grpcCallOptions = DaprClientUtilities.ConfigureGrpcCallOptions(typeof(DaprJobsClient).Assembly, this.DaprApiToken, cancellationToken);
            var response = await Client.GetJobAlpha1Async(envelope, grpcCallOptions);
            var schedule = DateTime.TryParse(response.Job.DueTime, out var dueTime)
                ? DaprJobSchedule.FromDateTime(dueTime)
                : new DaprJobSchedule(response.Job.Schedule);
                
            return new DaprJobDetails(schedule)
            {
                DueTime = !string.IsNullOrWhiteSpace(response.Job.DueTime) ? DateTime.Parse(response.Job.DueTime) : null,
                Ttl = !string.IsNullOrWhiteSpace(response.Job.Ttl) ? DateTime.Parse(response.Job.Ttl) : null,
                RepeatCount = (int?)response.Job.Repeats,
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
    [Experimental(DaprExperimentalConstants.JobsIdentifier)]
    public override async Task DeleteJobAsync(string jobName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobName))
        {
            throw new ArgumentNullException(nameof(jobName));
        }

        try
        {
            var envelope = new Autogenerated.DeleteJobRequest { Name = jobName };
            var grpcCallOptions = DaprClientUtilities.ConfigureGrpcCallOptions(typeof(DaprJobsClient).Assembly, this.DaprApiToken, cancellationToken);
            await Client.DeleteJobAlpha1Async(envelope, grpcCallOptions);
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
            this.HttpClient.Dispose();
        }
    }
}
