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

using Dapr.VirtualActors;

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Represents the registration of a single actor type with the runtime.
/// </summary>
/// <remarks>
/// Contains the actor type metadata and an AOT-safe factory delegate for
/// constructing actor instances without reflection.
/// </remarks>
/// <param name="TypeInformation">The type metadata for the registered actor.</param>
/// <param name="Factory">
/// An AOT-safe factory delegate that creates actor instances given a
/// <see cref="VirtualActorHost"/> and <see cref="IServiceProvider"/>.
/// Generated at compile time by the source generator, or provided explicitly
/// during manual registration.
/// </param>
public sealed record ActorRegistration(
    ActorTypeInformation TypeInformation,
    Func<VirtualActorHost, IServiceProvider, VirtualActor> Factory);

