using Dapr.Actors.Next.Core.Client;
using Dapr.Actors.Next.Core.Runtime;
using Dapr.Actors.Next.Core.Serialization;
using Dapr.Actors.Next.Core.State;
using Dapr.Actors.Next.Core.Timers;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.Actors.Next.Core.Test;

internal static class InMemoryActorAdapterServiceCollectionExtensions
{
    public static IServiceCollection AddInMemoryActorAdapters(this IServiceCollection services)
    {
        services.AddSingleton<IActorStateStore, InMemoryActorStateStore>();
        services.AddSingleton<IActorInvocationClient>(sp => sp.GetRequiredService<IActorRuntime>());
        services.AddSingleton<IActorTimerScheduler>(sp => new CoreActorTimerScheduler(
            sp.GetRequiredService<IActorRuntime>(),
            sp.GetRequiredService<IActorWireSerializer>(),
            sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IActorReminderScheduler>(sp => new CoreActorReminderScheduler(
            sp.GetRequiredService<IActorRuntime>(),
            sp.GetRequiredService<IActorWireSerializer>(),
            sp.GetRequiredService<TimeProvider>()));
        return services;
    }
}
