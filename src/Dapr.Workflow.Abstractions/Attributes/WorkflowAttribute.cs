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

namespace Dapr.Workflow.Abstractions.Attributes;

/// <summary>
/// Marks a class as a workflow implementation.
/// </summary>
/// <remarks>
/// This attribute can be used by source generators to automatically discover and register workflows
/// as compile time. It can also be used for runtime discovery and validation.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WorkflowAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowAttribute"/> class.
    /// </summary>
    public WorkflowAttribute()
    {
    }

    /// <summary>
    /// Initialies a new instance of the <see cref="WorkflowAttribute"/> class with a specific name.
    /// </summary>
    /// <param name="name">The name to reigster this workflow under. If not specified, the class name is used.</param>
    public WorkflowAttribute(string name)
    {
        Name = name;
    }
    
    /// <summary>
    /// Gets the name to register this workflow under.
    /// </summary>
    public string? Name { get; }
}
