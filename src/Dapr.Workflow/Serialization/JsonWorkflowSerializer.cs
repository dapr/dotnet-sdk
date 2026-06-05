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
//  ------------------------------------------------------------------------

using System;
using System.Text.Json;
using Dapr.Common.Serialization;

namespace Dapr.Workflow.Serialization;

/// <summary>
/// JSON-based implementation of <see cref="IWorkflowSerializer"/> using System.Text.Json.
/// </summary>
/// <remarks>
/// This class extends <see cref="JsonDaprSerializer"/> from <c>Dapr.Common</c> and exists for
/// backward compatibility. New code should prefer <see cref="JsonDaprSerializer"/> directly.
/// </remarks>
[Obsolete("The JsonWorkflowSerializer has been deprecated in favor of Dapr.Common.JsonDaprSerializer and will be removed with the SDK release coinciding with the release of the Dapr v1.20 runtime.")]
public sealed class JsonWorkflowSerializer : JsonDaprSerializer, IWorkflowSerializer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWorkflowSerializer"/> class with default JSON options.
    /// </summary>
    public JsonWorkflowSerializer()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonWorkflowSerializer"/> class with custom JSON options.
    /// </summary>
    /// <param name="options">The JSON serializer options to use for all serialization operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
    public JsonWorkflowSerializer(JsonSerializerOptions options) : base(options)
    {
    }
}
