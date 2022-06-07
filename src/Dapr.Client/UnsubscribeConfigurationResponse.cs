using System;
namespace Dapr.Client
{
    /// <summary>
    /// Response from an Unsubscribe Configuration call.
    /// </summary>
    public class UnsubscribeConfigurationResponse
    {
        /// <summary>
        /// Boolean representing if the request was successful or not.
        /// </summary>
        public bool Ok { get; }

        /// <summary>
        /// The message from the Configuration API.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="ok">Boolean indicating success.</param>
        /// <param name="message">Message from the Configuration API.</param>
        public UnsubscribeConfigurationResponse(bool ok, string message)
        {
            Ok = ok;
            Message = message;
        }
    }
}
