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
/// Marks a class as a workflow activity implementation. 
/// </summary>
/// <remarks>
/// This attribute can be used by source generators to automatically discovery and register
/// activities at compile time. It can also be used for runtime discovery and validation.
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class WorkflowActivityAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowActivityAttribute"/> class.
    /// </summary>
    public WorkflowActivityAttribute()
    {
    }
    
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowActivityAttribute"/> with a specific name.
    /// </summary>
    /// <param name="name">The name to register this activity under. If not specified, the class name
    /// is used.</param>
    public WorkflowActivityAttribute(string name)
    {
        Name = name;
    }
    
    /// <summary>
    /// Get the name to register this activity under.
    /// </summary>
    public string? Name { get; }
}
