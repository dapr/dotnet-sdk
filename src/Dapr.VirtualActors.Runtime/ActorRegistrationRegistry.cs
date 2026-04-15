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

using System.Collections.Concurrent;

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Thread-safe registry mapping actor type names to their registrations including
/// AOT-safe factory delegates.
/// </summary>
/// <remarks>
/// Populated during service configuration (from <see cref="VirtualActorOptions"/>
/// registrations) and queried at activation time by the
/// <see cref="DependencyInjectionActorActivator"/>.
/// </remarks>
internal sealed class ActorRegistrationRegistry
{
    private readonly ConcurrentDictionary<string, ActorRegistration> _registrations = new(StringComparer.Ordinal);

    /// <summary>
    /// Registers an actor type with its factory delegate.
    /// </summary>
    /// <param name="registration">The actor registration.</param>
    public void Register(ActorRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        if (!_registrations.TryAdd(registration.TypeInformation.ActorTypeName, registration))
        {
            throw new InvalidOperationException(
                $"Actor type '{registration.TypeInformation.ActorTypeName}' is already registered.");
        }
    }

    /// <summary>
    /// Gets the registration for the specified actor type name.
    /// </summary>
    /// <param name="actorTypeName">The actor type name.</param>
    /// <returns>The actor registration.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the actor type is not registered.
    /// </exception>
    public ActorRegistration GetRegistration(string actorTypeName)
    {
        if (_registrations.TryGetValue(actorTypeName, out var registration))
        {
            return registration;
        }

        throw new InvalidOperationException(
            $"Actor type '{actorTypeName}' is not registered. " +
            $"Call options.RegisterActor<T>() in AddDaprVirtualActors() to register it.");
    }

    /// <summary>
    /// Gets all registered actor type names.
    /// </summary>
    public IReadOnlyCollection<string> RegisteredActorTypes => _registrations.Keys.ToList();

    /// <summary>
    /// Gets all registrations.
    /// </summary>
    public IReadOnlyCollection<ActorRegistration> Registrations => _registrations.Values.ToList();
}
