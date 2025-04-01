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

using System;
using System.Collections.Generic;

namespace Dapr.Client;

/// <summary>
/// Represents the request used to invoke a binding.
/// </summary>
/// <param name="bindingName">The name of the binding.</param>
/// <param name="operation">The type of operation to perform on the binding.</param>
public sealed class BindingRequest(string bindingName, string operation)
{
    /// <summary>
    /// Gets the name of the binding.
    /// </summary>
    /// <value></value>
    public string BindingName { get; } = bindingName ?? throw new ArgumentNullException(nameof(bindingName));

    /// <summary>
    /// Gets the type of operation to perform on the binding.
    /// </summary>
    public string Operation { get; } = operation ?? throw new ArgumentNullException(nameof(operation));
    
    /// <summary>
    /// Gets or sets the binding request payload.
    /// </summary>
    public ReadOnlyMemory<byte> Data { get; set; }

    /// <summary>
    /// Gets the metadata; a collection of metadata key-value pairs that will be provided to the binding. 
    /// The valid metadata keys and values are determined by the type of binding used.
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = new();
}
