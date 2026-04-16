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

using System;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Runtime;

namespace Dapr.IntegrationTest.Actors.Serialization;

/// <summary>
/// Implementation of <see cref="ISerializationActor"/> that echoes its inputs back
/// to the caller, allowing tests to verify custom JSON serializer round-trips.
/// </summary>
public class SerializationActor(ActorHost host) : Actor(host), ISerializationActor
{
    /// <inheritdoc />
    public Task Ping(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<SerializationPayload> SendAsync(
        string name,
        SerializationPayload payload,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(payload);

    /// <inheritdoc />
    public Task<DateTime> AnotherMethod(DateTime payload) =>
        Task.FromResult(payload);
}
