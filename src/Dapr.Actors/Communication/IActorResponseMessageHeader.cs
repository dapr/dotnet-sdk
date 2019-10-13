// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    /// <summary>
    /// Defines an interfaces that must be implemented to provide header for remoting response message.
    ///
    /// </summary>
    public interface IActorResponseMessageHeader
    {
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

        /// <summary>
        /// Return true if no header exists , else false.
        /// </summary>
        /// <returns>true or false.</returns>
        bool CheckIfItsEmpty();
    }
}
