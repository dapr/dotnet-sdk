// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Communication;

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