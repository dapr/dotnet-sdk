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

namespace Dapr.Actors.Next.Abstractions.Registry;

/// <summary>
/// Invokes actors whose compile-time interface is not known by the caller.
/// </summary>
public interface IDynamicActorClient
{
    /// <summary>
    /// Invokes an actor with JSON arguments and returns a JSON result.
    /// </summary>
    Task<string?> InvokeAsync(string actorType, string actorId, string method, string argsJson, CancellationToken cancellationToken = default);
}
