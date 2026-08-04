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

using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Abstractions.Options;

/// <summary>
/// Stores actor types explicitly requested by the app.
/// </summary>
public sealed class DaprActorRegistrationCollection
{
    private readonly Dictionary<Type, DaprActorRegistration> registrations = new();

    /// <summary>
    /// Registers an actor type for hosting.
    /// </summary>
    public void RegisterActor<TActor>(string? actorTypeName = null)
        where TActor : IActor
    {
        if (actorTypeName is not null && string.IsNullOrWhiteSpace(actorTypeName))
        {
            throw new ArgumentException("Actor type name cannot be empty.", nameof(actorTypeName));
        }

        registrations[typeof(TActor)] = new DaprActorRegistration(typeof(TActor), actorTypeName);
    }

    /// <summary>
    /// Registers an actor type for hosting with per-type runtime overrides.
    /// </summary>
    public void RegisterActor<TActor>(Action<DaprActorTypeOptions> configure)
        where TActor : IActor
        => RegisterActor<TActor>(actorTypeName: null, configure);

    /// <summary>
    /// Registers an actor type for hosting under an explicit name with per-type runtime overrides.
    /// </summary>
    public void RegisterActor<TActor>(string? actorTypeName, Action<DaprActorTypeOptions> configure)
        where TActor : IActor
    {
        ArgumentNullException.ThrowIfNull(configure);
        if (actorTypeName is not null && string.IsNullOrWhiteSpace(actorTypeName))
        {
            throw new ArgumentException("Actor type name cannot be empty.", nameof(actorTypeName));
        }

        var typeOptions = new DaprActorTypeOptions();
        configure(typeOptions);
        registrations[typeof(TActor)] = new DaprActorRegistration(typeof(TActor), actorTypeName) { TypeOptions = typeOptions };
    }

    /// <summary>
    /// Gets the explicit registrations.
    /// </summary>
    public IReadOnlyCollection<DaprActorRegistration> Registrations => registrations.Values;

    /// <summary>
    /// Finds an explicit registration for an actor implementation type.
    /// </summary>
    public DaprActorRegistration? Find(Type actorImplementationType)
    {
        ArgumentNullException.ThrowIfNull(actorImplementationType);
        return registrations.GetValueOrDefault(actorImplementationType);
    }

    internal void CopyFrom(DaprActorRegistrationCollection source)
    {
        ArgumentNullException.ThrowIfNull(source);

        registrations.Clear();
        foreach (var registration in source.registrations)
        {
            registrations[registration.Key] = registration.Value;
        }
    }
}

