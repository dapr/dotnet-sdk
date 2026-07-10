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

namespace Dapr.Actors.Next.Core.Activation;

/// <summary>
/// Holds generated lifecycle delegates for an actor type.
/// </summary>
public sealed class ActorLifecycle(
    Func<IActor, CancellationToken, ValueTask> onActivateAsync,
    Func<IActor, CancellationToken, ValueTask> onDeactivateAsync,
    Func<IActor, ActorMethodContext, CancellationToken, ValueTask> onPreActorMethodAsync,
    Func<IActor, ActorMethodContext, Exception?, CancellationToken, ValueTask> onPostActorMethodAsync)
{
    /// <summary>
    /// Gets an empty lifecycle invoker.
    /// </summary>
    public static ActorLifecycle Empty { get; } = new(
        static (_, _) => ValueTask.CompletedTask,
        static (_, _) => ValueTask.CompletedTask,
        static (_, _, _) => ValueTask.CompletedTask,
        static (_, _, _, _) => ValueTask.CompletedTask);

    /// <summary>
    /// Gets the activation callback.
    /// </summary>
    public Func<IActor, CancellationToken, ValueTask> OnActivateAsync { get; } =
        onActivateAsync ?? throw new ArgumentNullException(nameof(onActivateAsync));

    /// <summary>
    /// Gets the deactivation callback.
    /// </summary>
    public Func<IActor, CancellationToken, ValueTask> OnDeactivateAsync { get; } =
        onDeactivateAsync ?? throw new ArgumentNullException(nameof(onDeactivateAsync));

    /// <summary>
    /// Gets the pre-method callback.
    /// </summary>
    public Func<IActor, ActorMethodContext, CancellationToken, ValueTask> OnPreActorMethodAsync { get; } =
        onPreActorMethodAsync ?? throw new ArgumentNullException(nameof(onPreActorMethodAsync));

    /// <summary>
    /// Gets the post-method callback.
    /// </summary>
    public Func<IActor, ActorMethodContext, Exception?, CancellationToken, ValueTask> OnPostActorMethodAsync { get; } =
        onPostActorMethodAsync ?? throw new ArgumentNullException(nameof(onPostActorMethodAsync));
}
