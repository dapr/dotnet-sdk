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

using Microsoft.Extensions.DependencyInjection;

namespace Dapr.VirtualActors.Runtime;

/// <summary>
/// Default <see cref="IActorActivator"/> that uses DI to construct actor instances
/// via pre-registered factory delegates (AOT-safe, no reflection).
/// </summary>
/// <remarks>
/// <para>
/// Creates a scoped <see cref="IServiceProvider"/> per actor activation so scoped
/// services are isolated per actor instance. Actor construction is performed by
/// a <see cref="Func{VirtualActorHost, IServiceProvider, VirtualActor}"/> factory
/// delegate that is registered at compile time (via source generator) or explicitly
/// during actor registration.
/// </para>
/// <para>
/// This activator never uses reflection — all actor construction is done through
/// strongly-typed factory delegates, making it fully AOT-compatible.
/// </para>
/// </remarks>
internal sealed class DependencyInjectionActorActivator : IActorActivator
{
    private readonly IServiceProvider _serviceProvider;

    public DependencyInjectionActorActivator(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task<ActorActivationResult> CreateAsync(
        ActorTypeInformation actorType,
        VirtualActorId actorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actorType);

        var scope = _serviceProvider.CreateAsyncScope();
        try
        {
            var host = scope.ServiceProvider.GetRequiredService<VirtualActorHostFactory>()
                .Create(actorType, actorId);

            // Use the pre-registered factory delegate — no reflection, AOT-safe.
            // The factory is registered during AddDaprVirtualActors() or by a source generator.
            var registration = scope.ServiceProvider.GetRequiredService<ActorRegistrationRegistry>()
                .GetRegistration(actorType.ActorTypeName);

            var actor = registration.Factory(host, scope.ServiceProvider);

            return Task.FromResult(new ActorActivationResult(actor, scope));
        }
        catch
        {
            // Dispose scope if activation fails
            _ = scope.DisposeAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(ActorActivationResult result, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Actor is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else if (result.Actor is IDisposable disposable)
        {
            disposable.Dispose();
        }

        if (result.Scope is not null)
        {
            await result.Scope.DisposeAsync();
        }
    }
}
