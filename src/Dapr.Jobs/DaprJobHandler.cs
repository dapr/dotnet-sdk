using Dapr.Jobs.Models.Responses;

namespace Dapr.Jobs;

/// <summary>
/// The delegate representing how a triggered job is handled.
/// </summary>
/// <param name="serviceProvider">The service provider that facilitates injecting resources to the delegate.</param>
/// <param name="jobName">The name of the triggered job.</param>
/// <param name="jobDetails">The details of the triggered job.</param>
/// <returns></returns>
public delegate Task InjectableDaprJobHandler(IServiceProvider serviceProvider, string? jobName, DaprJobDetails? jobDetails );

/// <summary>
/// The delegate representing how a triggered job is handled.
/// </summary>
/// <param name="jobName">The name of the triggered job.</param>
/// <param name="jobDetails">The details of the triggered job.</param>
/// <returns></returns>
public delegate Task DaprJobHandler(string? jobName, DaprJobDetails? jobDetails);

