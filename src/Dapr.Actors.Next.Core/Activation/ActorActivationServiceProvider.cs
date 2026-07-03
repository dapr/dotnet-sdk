using Dapr.Actors.Next.Abstractions;
using Dapr.Actors.Next.Abstractions.State;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Core.Activation;

/// <summary>
/// Exposes activation-scoped runtime services to hand-written and generated factories, layering the
/// activation built-ins (<see cref="ActorActivationContext"/>, <see cref="IActorStateAccessor"/>,
/// <see cref="ActorId"/>) over a per-activation dependency injection scope.
/// </summary>
/// <remarks>
/// The dependency injection scope is created lazily on the first resolution that falls through to the
/// container. Actors whose constructors depend only on the activation built-ins therefore never pay for a
/// scope allocation. When a scope is created it is owned by this instance and disposed when the activation
/// is deactivated, preserving the disposal semantics of scoped and transient services.
/// </remarks>
internal sealed class ActorActivationServiceProvider : IServiceProvider, IAsyncDisposable
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ActorActivationContext context;
    private readonly object gate = new();
    private AsyncServiceScope scope;
    private bool scopeCreated;
    private bool disposed;

    public ActorActivationServiceProvider(IServiceScopeFactory scopeFactory, ActorActivationContext context)
    {
        this.scopeFactory = scopeFactory;
        this.context = context;
    }

    public object? GetService(Type serviceType)
    {
        if (serviceType == typeof(ActorActivationContext))
        {
            return context;
        }

        if (serviceType == typeof(IActorStateAccessor))
        {
            return context.State;
        }

        if (serviceType == typeof(ActorId))
        {
            return context.ActorId;
        }

        return GetOrCreateScope().GetService(serviceType);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        bool disposeScope;
        AsyncServiceScope toDispose;
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            disposeScope = scopeCreated;
            toDispose = scope;
        }

        if (disposeScope)
        {
            await toDispose.DisposeAsync().ConfigureAwait(false);
        }
    }

    private IServiceProvider GetOrCreateScope()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!scopeCreated)
            {
                scope = scopeFactory.CreateAsyncScope();
                scopeCreated = true;
            }

            return scope.ServiceProvider;
        }
    }
}
