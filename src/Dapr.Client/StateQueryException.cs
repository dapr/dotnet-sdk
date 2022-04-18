using System.Collections.Generic;

namespace Dapr.Client
{
    /// <summary>
    /// Exception that is thrown when an erorr is encountered during a call to the Query API.
    /// This exception contains the partial results (if any) from that exception.
    /// </summary>
    public class StateQueryException<TValue> : DaprApiException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">The description of the exception from the source.</param>
        /// <param name="response">The response containing successful items, if any, in a response with errors.</param>
        /// <param name="failedKeys">The key(s) that encountered an error during the query.</param>
        public StateQueryException(string message, StateQueryResponse<TValue> response, IReadOnlyList<string> failedKeys)
            : base(message)
        {
            Response = response;
            FailedKeys = failedKeys;
        }

        /// <summary>
        /// The response containing successful items, if any, in a response with errors.
        /// </summary>
        public StateQueryResponse<TValue> Response { get; }

        /// <summary>
        /// The key(s) that encountered an error during the query.
        /// </summary>
        public IReadOnlyList<string> FailedKeys { get; }
    }
}
