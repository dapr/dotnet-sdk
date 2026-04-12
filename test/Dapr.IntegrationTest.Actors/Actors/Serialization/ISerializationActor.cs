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
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors.Serialization;

/// <summary>
/// A round-trip payload used to validate custom JSON serialization via actor remoting.
/// </summary>
/// <param name="Message">The primary message string.</param>
public record SerializationPayload(string Message)
{
    /// <summary>Gets or sets an arbitrary JSON element carried inside the payload.</summary>
    public JsonElement Value { get; set; }

    /// <summary>Gets or sets extension data that should survive a round-trip through the actor runtime.</summary>
    [JsonExtensionData]
    public Dictionary<string, object>? ExtensionData { get; set; }
}

/// <summary>
/// Actor interface that validates custom JSON serialization behaviour when invoking actor methods.
/// </summary>
public interface ISerializationActor : IActor, IPingActor
{
    /// <summary>
    /// Echoes <paramref name="payload"/> back to the caller to verify that custom
    /// JSON serializer options are respected during remoting.
    /// </summary>
    /// <param name="name">An arbitrary label for the operation.</param>
    /// <param name="payload">The payload to echo.</param>
    /// <param name="cancellationToken">A token to cancel the call.</param>
    Task<SerializationPayload> SendAsync(string name, SerializationPayload payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Echoes <paramref name="payload"/> as a <see cref="DateTime"/> to verify that
    /// multiple method overloads are dispatched correctly with the custom serializer.
    /// </summary>
    /// <param name="payload">The date/time value to echo.</param>
    Task<DateTime> AnotherMethod(DateTime payload);
}
