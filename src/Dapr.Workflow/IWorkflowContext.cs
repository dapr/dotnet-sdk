namespace Dapr.Workflow;

/// <summary>
/// Provides functionality available to orchestration code.
/// </summary>
public interface IWorkflowContext
{
    /// <summary>
    /// Gets a value indicating whether the orchestration or operation is currently replaying itself.
    /// </summary>
    /// <remarks>
    /// This property is useful when there is logic that needs to run only when *not* replaying. For example,
    /// certain types of application logging may become too noisy when duplicated as part of replay. The
    /// application code could check to see whether the function is being replayed and then issue
    /// the log statements when this value is <c>false</c>.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the orchestration or operation is currently being replayed; otherwise <c>false</c>.
    /// </value>
    bool IsReplaying { get; }
}
