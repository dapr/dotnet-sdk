// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    /// <summary>
    /// Specifies the headers that are sent along with a request message.
    /// </summary>
    public interface IActorRequestMessageHeader
    {
        /// <summary>
        /// Gets or sets the actorId to which remoting request will dispatch to.
        /// </summary>
        ActorId ActorId { get; set; }

        /// <summary>
        /// Gets or sets the actorType to which remoting request will dispatch to.
        /// </summary>
        string ActorType { get; set; }

        /// <summary>
        /// Gets or sets the call context which is used to limit re-eentrancy in Actors.
        /// </summary>
        string CallContext { get; set; }

        /// <summary>
        /// Gets or sets the methodId of the remote method.
        /// </summary>
        /// <value>The method id.</value>
        int MethodId { get; set; }

        /// <summary>
        /// Gets or sets the interface id of the remote interface.
        /// </summary>
        /// <value>The interface id.</value>
        int InterfaceId { get; set; }

        /// <summary>
        /// Gets or sets the identifier for the remote method invocation.
        /// </summary>
        string InvocationId { get; set; }

        /// <summary>
        /// Gets or sets the Method Name  of the remoting method.
        /// </summary>
        string MethodName { get; set; }

        /// <summary>
        /// Adds a new header with the specified name and value.
        /// </summary>
        /// <param name="headerName">The header Name.</param>
        /// <param name="headerValue">The header value.</param>
        void AddHeader(string headerName, byte[] headerValue);

        /// <summary>
        /// Gets the header with the specified name.
        /// </summary>
        /// <param name="headerName">The header Name.</param>
        /// <param name="headerValue">The header value.</param>
        /// <returns>true if a header with that name exists; otherwise, false.</returns>
        bool TryGetHeaderValue(string headerName, out byte[] headerValue);
    }
}
