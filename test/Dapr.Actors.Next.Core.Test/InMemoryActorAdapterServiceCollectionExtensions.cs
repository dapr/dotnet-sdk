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
