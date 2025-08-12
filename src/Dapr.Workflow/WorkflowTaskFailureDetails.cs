// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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
using Dapr.DurableTask;

/// <summary>
/// Represents workflow task failure details.
/// </summary>
public class WorkflowTaskFailureDetails
{
    readonly TaskFailureDetails details;

    internal WorkflowTaskFailureDetails(TaskFailureDetails details)
    {
        this.details = details ?? throw new ArgumentNullException(nameof(details));
    }

    /// <summary>
    /// Gets the error type, which is the namespace-qualified exception type name.
    /// </summary>
    public string ErrorType => this.details.ErrorType;

    /// <summary>
    /// Gets a summary description of the failure, which is typically an exception message.
    /// </summary>
    public string ErrorMessage => this.details.ErrorMessage;

    /// <summary>
    /// Gets the stack trace of the failure.
    /// </summary>
    public string? StackTrace => this.details.StackTrace;

    /// <summary>
    /// Returns <c>true</c> if the failure was caused by the specified exception type.
    /// </summary>
    /// <remarks>
    /// This method allows checking if a workflow task failed due to an exception of a specific type by attempting
    /// to load the type specified in <see cref="ErrorType"/>. If the exception type cannot be loaded
    /// for any reason, this method will return <c>false</c>. Base types are supported.
    /// </remarks>
    /// <typeparam name="T">The type of exception to test against.</typeparam>
    /// <returns>
    /// Returns <c>true</c> if the <see cref="ErrorType"/> value matches <typeparamref name="T"/>; <c>false</c> otherwise.
    /// </returns>
    public bool IsCausedBy<T>() where T : Exception
    {
        return this.details.IsCausedBy<T>();
    }

    /// <summary>
    /// Gets a debug-friendly description of the failure information.
    /// </summary>
    /// <returns>A debugger friendly display string.</returns>
    public override string ToString()
    {
        return $"{this.ErrorType}: {this.ErrorMessage}";
    }
}
