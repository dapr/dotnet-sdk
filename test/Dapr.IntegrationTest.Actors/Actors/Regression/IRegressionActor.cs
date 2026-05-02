// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors.Regression;

/// <summary>
/// Describes a state operation to be executed by <see cref="IRegressionActor"/>.
/// </summary>
public sealed class StateCall
{
    /// <summary>
    /// Gets or sets the state key to operate on.
    /// </summary>
    public string? Key { get; set; }

    /// <summary>
    /// Gets or sets the value to write, if applicable.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the operation to perform.
    /// Valid values are <c>"SetState"</c>, <c>"SaveState"</c>, and <c>"ThrowException"</c>.
    /// </summary>
    public string? Operation { get; set; }
}

/// <summary>
/// Actor interface used to reproduce regression #762, which validated that an exception
/// thrown mid-method correctly rolls back pending state changes instead of persisting them.
/// </summary>
public interface IRegressionActor : IPingActor, IActor
{
    /// <summary>
    /// Returns the value stored under <paramref name="id"/>, or an empty string when absent.
    /// </summary>
    /// <param name="id">The state key to retrieve.</param>
    Task<string> GetState(string id);

    /// <summary>
    /// Executes the state operation described by <paramref name="call"/>.
    /// Throws when <see cref="StateCall.Operation"/> is <c>"ThrowException"</c>.
    /// </summary>
    /// <param name="call">The operation to execute.</param>
    Task SaveState(StateCall call);

    /// <summary>
    /// Removes the state entry identified by <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The state key to remove.</param>
    Task RemoveState(string id);
}
