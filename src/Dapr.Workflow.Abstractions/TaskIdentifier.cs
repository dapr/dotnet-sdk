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

namespace Dapr.Workflow.Abstractions;

/// <summary>
/// Identifies a workflow task (workflow or activity).
/// </summary>
/// <param name="Name">The name of the task.</param>
public readonly record struct TaskIdentifier(string Name)
{
    /// <summary>
    /// Implicitly converts a string to a <see cref="TaskIdentifier"/>.
    /// </summary>
    /// <param name="name">The task name.</param>
    public static implicit operator TaskIdentifier(string name) => new (name);

    /// <summary>
    /// Implicitly converts a <see cref="TaskIdentifier"/> to a string.
    /// </summary>
    /// <param name="identifier">The task identifier.</param>
    public static implicit operator string(TaskIdentifier identifier) => identifier.Name;
    
    /// <inheritdoc />
    public override string ToString() => Name;
}
