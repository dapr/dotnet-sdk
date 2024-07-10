﻿namespace Dapr.Jobs;

/// <summary>
/// Describes an endpoint as a subscriber for a job invocation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class JobAttribute : Attribute
{
    /// <summary>
    /// The name of the job.
    /// </summary>
    public string JobName { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobAttribute"/> class.
    /// </summary>
    /// <param name="jobName">The name of the job that invokes this method.</param>
    public JobAttribute(string jobName)
    {
        JobName = jobName;
    }
}
