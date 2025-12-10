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

using System;
using System.Text.Json;

namespace Dapr.Workflow.Client;

/// <summary>
/// Metadata about a workflow instance.
/// </summary>
/// <param name="InstanceId">The instance ID of the workflow.</param>
/// <param name="Name">The name of the workflow.</param>
/// <param name="RuntimeStatus">The runtime status of the workflow.</param>
/// <param name="CreatedAt">The time when the workflow was created.</param>
/// <param name="LastUpdatedAt">The time when the workflow last updated.</param>
public sealed record WorkflowMetadata(
    string InstanceId,
    string Name,
    WorkflowRuntimeStatus RuntimeStatus,
    DateTime CreatedAt,
    DateTime LastUpdatedAt)
{
    /// <summary>
    /// Gets the serialized input of the workflow, if available.
    /// </summary>
    public string? SerializedInput { get; init; }

    /// <summary>
    /// Gets the serialized output of the workflow, if available.
    /// </summary>
    public string? SerializedOutput { get; init; }

    /// <summary>
    /// Gets the serialized custom status of the workflow, if available.
    /// </summary>
    public string? SerializedCustomStatus { get; init; }

    /// <summary>
    /// Gets the failure details if the workflow failed.
    /// </summary>
    public WorkflowTaskFailureDetails? FailureDetails { get; init; }

    /// <summary>
    /// Gets a value indicating whether the workflow instance exists.
    /// </summary>
    public bool Exists => !string.IsNullOrEmpty(InstanceId);

    /// <summary>
    /// Deserializes the workflow input.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The deserialized input, or default if not available.</returns>
    public T? ReadInputAs<T>() => string.IsNullOrEmpty(SerializedInput) ? default : JsonSerializer.Deserialize<T>(SerializedInput);

    /// <summary>
    /// Deserializes the workflow output.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The deserialized output, or default if not available.</returns>
    public T? ReadOutputAs<T>() => string.IsNullOrEmpty(SerializedOutput) ? default : JsonSerializer.Deserialize<T>(SerializedOutput);

    /// <summary>
    /// Deserializes the custom status.
    /// </summary>
    /// <typeparam name="T">The type to deserialize to.</typeparam>
    /// <returns>The deserialized custom status, or default if not available.</returns>
    public T? ReadCustomStatusAs<T>() => string.IsNullOrEmpty(SerializedCustomStatus) ? default : JsonSerializer.Deserialize<T>(SerializedCustomStatus);
}
