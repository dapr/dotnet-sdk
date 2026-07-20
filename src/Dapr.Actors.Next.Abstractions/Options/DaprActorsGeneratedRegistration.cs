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

using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Abstractions.Options;

/// <summary>
/// Stores generated registration callbacks used by <see cref="DaprActorsServiceCollectionExtensions"/>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DaprActorsGeneratedRegistration
{
    private static readonly object SyncRoot = new();
    private static readonly List<Action<IServiceCollection, DaprActorsOptions>> Registrations = [];

    /// <summary>
    /// Registers a generated service registration callback.
    /// </summary>
    public static void Register(Action<IServiceCollection> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        Register((services, _) => registration(services));
    }

    /// <summary>
    /// Registers a generated service registration callback.
    /// </summary>
    public static void Register(Action<IServiceCollection, DaprActorsOptions> registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        lock (SyncRoot)
        {
            Registrations.Add(registration);
        }
    }

    /// <summary>
    /// Applies every generated service registration callback.
    /// </summary>
    public static void Apply(IServiceCollection services)
    {
        Apply(services, new DaprActorsOptions());
    }

    /// <summary>
    /// Applies every generated service registration callback.
    /// </summary>
    public static void Apply(IServiceCollection services, DaprActorsOptions options)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        Action<IServiceCollection, DaprActorsOptions>[] registrations;

        lock (SyncRoot)
        {
            registrations = Registrations.ToArray();
        }

        foreach (var registration in registrations)
        {
            registration(services, options);
        }
    }
}
