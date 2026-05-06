// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.StateManagement;

/// <summary>
/// Options controlling the consistency and concurrency behavior of a Dapr state operation.
/// </summary>
public sealed class StateOptions
{
    /// <summary>
    /// Gets or sets the consistency mode for the state operation.
    /// When <see langword="null"/>, the Dapr runtime uses the state store's default consistency setting.
    /// </summary>
    public ConsistencyMode? Consistency { get; set; }

    /// <summary>
    /// Gets or sets the concurrency mode for the state operation.
    /// When <see langword="null"/>, the Dapr runtime uses the state store's default concurrency setting.
    /// </summary>
    public ConcurrencyMode? Concurrency { get; set; }
}
