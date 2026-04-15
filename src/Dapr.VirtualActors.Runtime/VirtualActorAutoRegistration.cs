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

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Static registry for source-generated actor discovery hooks.
/// </summary>
/// <remarks>
/// <para>
/// The source generator emits a <c>[ModuleInitializer]</c> that calls
/// <see cref="RegisterDiscoveryHook"/> at assembly load time. When
/// <see cref="VirtualActorServiceCollectionExtensions.AddDaprVirtualActors(Microsoft.Extensions.DependencyInjection.IServiceCollection, Action{VirtualActorOptions})"/>
/// configures options, it invokes all registered hooks to auto-register
/// discovered actor types — with no reflection and full AOT safety.
/// </para>
/// <para>
/// This two-phase approach (register hook at module init, apply at DI config time)
/// ensures that the options instance exists before factories are registered.
/// </para>
/// </remarks>
public static class VirtualActorAutoRegistration
{
    private static readonly List<Action<VirtualActorOptions>> Hooks = [];

    /// <summary>
    /// Registers a discovery hook. Called by source-generated module initializers.
    /// </summary>
    /// <param name="hook">A delegate that registers discovered actors into options.</param>
    public static void RegisterDiscoveryHook(Action<VirtualActorOptions> hook)
    {
        ArgumentNullException.ThrowIfNull(hook);
        lock (Hooks)
        {
            Hooks.Add(hook);
        }
    }

    /// <summary>
    /// Applies all registered discovery hooks to the given options instance.
    /// </summary>
    /// <param name="options">The options to populate with discovered actors.</param>
    internal static void ApplyDiscoveryHooks(VirtualActorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (Hooks)
        {
            foreach (var hook in Hooks)
            {
                hook(options);
            }
        }
    }
}
