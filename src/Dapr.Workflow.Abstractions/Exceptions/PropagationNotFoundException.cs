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

namespace Dapr.Workflow;

using System;

/// <summary>
/// Thrown when a query against propagated workflow history finds no match.
/// </summary>
/// <remarks>
/// Raised by <see cref="PropagatedHistory.GetLastWorkflowByName"/>,
/// <see cref="WorkflowResult.GetLastActivityByName"/>, and
/// <see cref="WorkflowResult.GetLastChildWorkflowByName"/> when the requested
/// name is not present in the propagated history chain. Use the plural
/// <c>Get*sByName</c> variants if you want an empty-list result instead.
/// </remarks>
public sealed class PropagationNotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance of <see cref="PropagationNotFoundException"/>.
    /// </summary>
    public PropagationNotFoundException()
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PropagationNotFoundException"/> with a message.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public PropagationNotFoundException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of <see cref="PropagationNotFoundException"/> with a message
    /// and inner exception.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The underlying cause.</param>
    public PropagationNotFoundException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
