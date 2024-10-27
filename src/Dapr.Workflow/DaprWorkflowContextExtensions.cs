using Microsoft.Extensions.Logging;

namespace Dapr.Workflow;

/// <summary>
/// Defines convenient overloads for calling context methods.
/// </summary>
public static class DaprWorkflowContextExtensions
{
    /// <summary>
    /// Returns an instance of <see cref="ILogger"/> that is replay safe, ensuring the logger logs only
    /// when the orchestrator is not replaying that line of code.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="logger">An instance of <see cref="ILogger"/>.</param>
    /// <returns>An instance of a replay-safe <see cref="ILogger"/>.</returns>
    public static ILogger CreateReplaySafeLogger(this IWorkflowContext context, ILogger logger) =>
        new ReplaySafeLogger(logger, context);
}
