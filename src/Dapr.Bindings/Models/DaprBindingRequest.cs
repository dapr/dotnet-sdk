// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

namespace Dapr.Bindings.Models;

/// <summary>
/// Represents the request used to invoke a binding.
/// </summary>
/// <param name="BindingName">The name of the binding.</param>
/// <param name="Operation">The type of the operation to perform on the binding.</param>
public sealed record DaprBindingRequest(string BindingName, string Operation )
{
    /// <summary>
    /// The binding request payload.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; init; } = default;

    /// <summary>
    /// The collection of metadata key/value pairs that will be provided to the binding.
    /// The valid metadata keys and values are determined by the type of binding used.
    /// </summary>
    public Dictionary<string, string> Metadata { get; init; } = [];
}
