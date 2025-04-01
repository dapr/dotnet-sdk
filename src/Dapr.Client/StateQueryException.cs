using System.Collections.Generic;

namespace Dapr.Client;

/// <summary>
/// Exception that is thrown when an erorr is encountered during a call to the Query API.
/// This exception contains the partial results (if any) from that exception.
/// </summary>
/// <param name="message">The description of the exception from the source.</param>
/// <param name="response">The response containing successful items, if any, in a response with errors.</param>
/// <param name="failedKeys">The key(s) that encountered an error during the query.</param>
public class StateQueryException<TValue>(string message, StateQueryResponse<TValue> response, IReadOnlyList<string> failedKeys) : DaprApiException(message)
{
    /// <summary>
    /// The response containing successful items, if any, in a response with errors.
    /// </summary>
    public StateQueryResponse<TValue> Response { get; } = response;

    /// <summary>
    /// The key(s) that encountered an error during the query.
    /// </summary>
    public IReadOnlyList<string> FailedKeys { get; } = failedKeys;
}
