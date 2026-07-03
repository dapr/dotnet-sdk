namespace Dapr.Actors.Next.StateMachine;

/// <summary>
/// Shared names used by the state-machine runtime.
/// </summary>
public static class StateMachineConstants
{
    /// <summary>
    /// Persisted state slot containing the current state, data, and durable deferred events.
    /// </summary>
    public const string EnvelopeStateName = "__stateMachine";

    /// <summary>
    /// Test-inspection state slot containing the current enum state.
    /// </summary>
    public const string CurrentStateStateName = "__currentState";

    /// <summary>
    /// Test-inspection state slot containing the current extended data.
    /// </summary>
    public const string DataStateName = "__data";

    /// <summary>
    /// Reserved actor operation used for state-machine timer callbacks.
    /// </summary>
    public const string TimerOperationName = "__stateMachineTimer";

    /// <summary>
    /// Reserved timer name used by declarative state timeouts.
    /// </summary>
    public const string StateTimeoutTimerName = "__stateTimeout";
}
