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
using System.Text.Json;

namespace Dapr.Client
{
    /// <summary>
    /// The response type for the <see cref="DaprClient.GetWorkflowAsync"/> API.
    /// </summary>
    public class GetWorkflowResponse
    {
        /// <summary>
        /// Gets the instance ID of the workflow.
        /// </summary>
        public string InstanceId { get; init; }
        
        /// <summary>
        /// Gets the name of the workflow.
        /// </summary>
        public string WorkflowName { get; init; }

        /// <summary>
        /// Gets the name of the workflow component.
        /// </summary>
        public string WorkflowComponentName { get; init; }

        /// <summary>
        /// Gets the time at which the workflow was created.
        /// </summary>
        public DateTime CreatedAt { get; init; }

        /// <summary>
        /// Gets the time at which the workflow was last updated.
        /// </summary>
        public DateTime LastUpdatedAt { get; init; }

        /// <summary>
        /// Gets the runtime status of the workflow.
        /// </summary>
        public WorkflowRuntimeStatus RuntimeStatus { get; init; }

        /// <summary>
        /// Gets the component-specific workflow properties.
        /// </summary>
        public IReadOnlyDictionary<string, string> Properties { get; init; }

        /// <summary>
        /// Gets the details associated with the workflow failure, if any.
        /// </summary>
        public WorkflowFailureDetails FailureDetails { get; init; }

        /// <summary>
        /// Deserializes the workflow input into <typeparamref name="T"/> using <see cref="JsonSerializer"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the workflow input into.</typeparam>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <returns>Returns the input as <typeparamref name="T"/>, or returns a default value if the workflow doesn't have an input.</returns>
        public T ReadInputAs<T>(JsonSerializerOptions options = null)
        {
            // FUTURE: Make this part of the protobuf contract instead of properties
            string defaultInputKey = $"{this.WorkflowComponentName}.workflow.input";
            if (!this.Properties.TryGetValue(defaultInputKey, out string serializedInput))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(serializedInput, options);
        }

        /// <summary>
        /// Deserializes the workflow output into <typeparamref name="T"/> using <see cref="JsonSerializer"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the workflow output into.</typeparam>
        /// <param name="options">Options to control the behavior during parsing.</param>
        /// <returns>Returns the output as <typeparamref name="T"/>, or returns a default value if the workflow doesn't have an output.</returns>
        public T ReadOutputAs<T>(JsonSerializerOptions options = null)
        {
            // FUTURE: Make this part of the protobuf contract instead of properties
            string defaultOutputKey = $"{this.WorkflowComponentName}.workflow.output";
            if (!this.Properties.TryGetValue(defaultOutputKey, out string serializedOutput))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(serializedOutput, options);
        }
    }
}
