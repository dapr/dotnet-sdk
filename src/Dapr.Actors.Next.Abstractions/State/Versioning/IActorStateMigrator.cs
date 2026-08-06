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

namespace Dapr.Actors.Next.Abstractions.State.Versioning;

/// <summary>
/// Runs before a migration hop is applied.
/// </summary>
public delegate ValueTask ActorStateUpcastHopCallback(
    Type fromStateType,
    Type toStateType,
    CancellationToken cancellationToken = default);

/// <summary>
/// Runtime-registrable actor state migration core populated by generated or hand-written metadata.
/// </summary>
public interface IActorStateMigrator
{
    /// <summary>
    /// Resolves the migration family that contains the requested target type.
    /// </summary>
    ActorStateMigrationFamily? ResolveFamily(Type targetType);

    /// <summary>
    /// Resolves the migration node for the requested target type.
    /// </summary>
    ActorStateMigrationNode? ResolveTargetNode(Type targetType);

    /// <summary>
    /// Migrates an enrolled payload from its stored chain node to the requested target type.
    /// </summary>
    ValueTask<TTarget> MigrateAsync<TTarget>(
        int chainIndex,
        string shapeHash,
        ReadOnlyMemory<byte> payload,
        IActorStateMigrationSerializer serializer,
        ActorStateUpcastHopCallback? beforeUpcastHop = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads plain state bytes as the requested type.
    /// </summary>
    ValueTask<TTarget> ReadPlainAsync<TTarget>(
        ReadOnlyMemory<byte> payload,
        IActorStateMigrationSerializer serializer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes an enrolled value with the requested node discriminator.
    /// </summary>
    ValueTask<byte[]> WriteEnvelopedAsync<T>(
        T value,
        ActorStateMigrationNode targetNode,
        IActorStateMigrationSerializer serializer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a value without a migration discriminator.
    /// </summary>
    ValueTask<byte[]> WritePlainAsync<T>(
        T value,
        IActorStateMigrationSerializer serializer,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Folds stored state to the requested type and writes it without a migration discriminator.
    /// </summary>
    ValueTask<byte[]> WriteGraduatedAsync<TTarget>(
        int chainIndex,
        string shapeHash,
        ReadOnlyMemory<byte> payload,
        IActorStateMigrationSerializer serializer,
        CancellationToken cancellationToken = default);
}
