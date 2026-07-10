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

using Dapr.Actors.Next.Abstractions.State;
using Dapr.Actors.Next.Abstractions.State.Versioning;

namespace Dapr.Actors.Next.Core.State.Versioning;

/// <summary>
/// Runtime actor state migration registry populated by generated or hand-written metadata.
/// </summary>
public sealed class ActorStateMigrationRegistry : IActorStateMigrator
{
    private readonly Dictionary<Type, FamilyRegistration> familiesByType = new();
    private readonly Dictionary<Type, ActorStateMigrationNode> nodesByType = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorStateMigrationRegistry"/> class.
    /// </summary>
    public ActorStateMigrationRegistry(IEnumerable<ActorStateMigrationFamilyRegistration> families)
    {
        ArgumentNullException.ThrowIfNull(families);

        foreach (var family in families)
        {
            var registration = new FamilyRegistration(family);
            foreach (var node in family.Metadata.Nodes)
            {
                if (!nodesByType.TryAdd(node.ClrType, node))
                {
                    throw new InvalidOperationException($"Actor state type '{node.ClrType.FullName}' is registered in more than one migration family.");
                }

                familiesByType.Add(node.ClrType, registration);
            }
        }
    }

    /// <inheritdoc />
    public ActorStateMigrationFamily? ResolveFamily(Type targetType) =>
        familiesByType.TryGetValue(targetType, out var family) ? family.Metadata : null;

    /// <inheritdoc />
    public ActorStateMigrationNode? ResolveTargetNode(Type targetType) =>
        nodesByType.TryGetValue(targetType, out var node) ? node : null;

    /// <inheritdoc />
    public async ValueTask<TTarget> MigrateAsync<TTarget>(
        int chainIndex,
        string shapeHash,
        ReadOnlyMemory<byte> payload,
        IActorStateMigrationSerializer serializer,
        ActorStateUpcastHopCallback? beforeUpcastHop = null,
        CancellationToken cancellationToken = default)
    {
        if (!familiesByType.TryGetValue(typeof(TTarget), out var family))
        {
            throw new InvalidOperationException($"Actor state type '{typeof(TTarget).FullName}' is not registered for migration.");
        }

        var sourceNode = family.ResolveNode(chainIndex);
        if (!string.Equals(sourceNode.ShapeHash, shapeHash, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Actor state shape drift detected for '{sourceNode.ClrType.FullName}' at chain index {chainIndex}.");
        }

        var current = family.Deserialize(chainIndex, payload, serializer);
        foreach (var hop in family.ResolvePath(chainIndex, typeof(TTarget)))
        {
            if (beforeUpcastHop is not null)
            {
                await beforeUpcastHop(hop.FromNode.ClrType, hop.ToNode.ClrType, cancellationToken).ConfigureAwait(false);
            }

            current = await hop.Upcast(current, cancellationToken).ConfigureAwait(false);
        }

        return current is TTarget typed
            ? typed
            : throw new InvalidOperationException($"Actor state migration did not produce '{typeof(TTarget).FullName}'.");
    }

    /// <inheritdoc />
    public ValueTask<TTarget> ReadPlainAsync<TTarget>(
        ReadOnlyMemory<byte> payload,
        IActorStateMigrationSerializer serializer,
        CancellationToken cancellationToken = default)
    {
        var envelope = serializer.DeserializeFromBytes<ActorStatePlainEnvelope<TTarget>>(payload);
        if (envelope is not null && envelope.Header.Magic == ActorStateEnvelopeHeader.CurrentMagic)
        {
            return ValueTask.FromResult(envelope.Value);
        }

        var value = serializer.DeserializeFromBytes<TTarget>(payload)
            ?? throw new InvalidOperationException($"State could not be deserialized as '{typeof(TTarget).FullName}'.");
        return ValueTask.FromResult(value);
    }

    /// <inheritdoc />
    public ValueTask<byte[]> WriteEnvelopedAsync<T>(
        T value,
        ActorStateMigrationNode targetNode,
        IActorStateMigrationSerializer serializer,
        CancellationToken cancellationToken = default)
    {
        var envelope = new ActorStateEnvelope<T>(
            ActorStateEnvelopeHeader.Create(ActorStateFormKind.Enveloped, serializer.SerializerId, serializer.SerializerVersion),
            new ActorStateDiscriminator(targetNode.Index, targetNode.ShapeHash),
            value);
        return ValueTask.FromResult(serializer.SerializeToBytes(envelope));
    }

    /// <inheritdoc />
    public ValueTask<byte[]> WritePlainAsync<T>(
        T value,
        IActorStateMigrationSerializer serializer,
        CancellationToken cancellationToken = default)
    {
        var envelope = new ActorStatePlainEnvelope<T>(
            ActorStateEnvelopeHeader.Create(ActorStateFormKind.Plain, serializer.SerializerId, serializer.SerializerVersion),
            value);
        return ValueTask.FromResult(serializer.SerializeToBytes(envelope));
    }

    /// <inheritdoc />
    public async ValueTask<byte[]> WriteGraduatedAsync<TTarget>(
        int chainIndex,
        string shapeHash,
        ReadOnlyMemory<byte> payload,
        IActorStateMigrationSerializer serializer,
        CancellationToken cancellationToken = default)
    {
        var value = await MigrateAsync<TTarget>(chainIndex, shapeHash, payload, serializer, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await WritePlainAsync(value, serializer, cancellationToken).ConfigureAwait(false);
    }

    private sealed class FamilyRegistration
    {
        private readonly Dictionary<int, ActorStateMigrationNode> nodes;
        private readonly Dictionary<int, ActorStateDeserializeDelegate> deserializers;
        private readonly Dictionary<(int From, int To), ActorStateUpcastDelegate> hops;
        private readonly Dictionary<(int From, Type Target), ActorStateUpcastStep[]> paths = new();

        public FamilyRegistration(ActorStateMigrationFamilyRegistration registration)
        {
            Metadata = registration.Metadata;
            nodes = Metadata.Nodes.ToDictionary(static node => node.Index);
            deserializers = registration.NodeDeserializers.ToDictionary(static item => item.ChainIndex, static item => item.Deserialize);
            hops = registration.HopDelegates.ToDictionary(static item => (item.FromIndex, item.ToIndex), static item => item.Upcast);

            foreach (var node in Metadata.Nodes)
            {
                if (!deserializers.ContainsKey(node.Index))
                {
                    throw new InvalidOperationException($"Actor state migration node {node.Index} in '{Metadata.CanonicalName}' has no deserializer.");
                }
            }

            ValidateEdges();
            BuildPaths();
        }

        public ActorStateMigrationFamily Metadata { get; }

        public ActorStateMigrationNode ResolveNode(int chainIndex) =>
            nodes.TryGetValue(chainIndex, out var node)
                ? node
                : throw new InvalidOperationException($"Unknown actor state migration chain index {chainIndex} in '{Metadata.CanonicalName}'.");

        public object Deserialize(int chainIndex, ReadOnlyMemory<byte> payload, IActorStateMigrationSerializer serializer) =>
            deserializers[chainIndex](payload, serializer)
            ?? throw new InvalidOperationException($"Actor state at chain index {chainIndex} in '{Metadata.CanonicalName}' could not be deserialized.");

        public IReadOnlyList<ActorStateUpcastStep> ResolvePath(int chainIndex, Type targetType)
        {
            var targetNode = Metadata.Nodes.SingleOrDefault(node => node.ClrType == targetType)
                ?? throw new InvalidOperationException($"Actor state type '{targetType.FullName}' is not in migration family '{Metadata.CanonicalName}'.");

            return paths.TryGetValue((chainIndex, targetType), out var path)
                ? path
                : throw new InvalidOperationException(
                    $"No actor state migration path exists from chain index {chainIndex} to '{targetType.FullName}' in '{Metadata.CanonicalName}'.");
        }

        private void ValidateEdges()
        {
            foreach (var edge in Metadata.Edges)
            {
                if (!nodes.ContainsKey(edge.FromIndex) || !nodes.ContainsKey(edge.ToIndex))
                {
                    throw new InvalidOperationException($"Actor state migration edge {edge.FromIndex}->{edge.ToIndex} in '{Metadata.CanonicalName}' references a missing node.");
                }

                if (!hops.ContainsKey((edge.FromIndex, edge.ToIndex)))
                {
                    throw new InvalidOperationException($"Actor state migration edge {edge.FromIndex}->{edge.ToIndex} in '{Metadata.CanonicalName}' has no hop delegate.");
                }
            }
        }

        private void BuildPaths()
        {
            foreach (var source in Metadata.Nodes)
            {
                foreach (var target in Metadata.Nodes)
                {
                    var path = FindUniquePath(source.Index, target.Index);
                    if (path is not null)
                    {
                        paths[(source.Index, target.ClrType)] = path;
                    }
                }
            }
        }

        private ActorStateUpcastStep[]? FindUniquePath(int sourceIndex, int targetIndex)
        {
            if (sourceIndex == targetIndex)
            {
                return [];
            }

            var results = new List<ActorStateUpcastStep[]>();
            Visit(sourceIndex, targetIndex, new HashSet<int>(), new List<ActorStateUpcastStep>(), results);
            return results.Count switch
            {
                1 => results[0],
                0 => null,
                _ => throw new InvalidOperationException($"More than one actor state migration path exists from {sourceIndex} to {targetIndex} in '{Metadata.CanonicalName}'."),
            };
        }

        private void Visit(
            int current,
            int target,
            HashSet<int> seen,
            List<ActorStateUpcastStep> path,
            List<ActorStateUpcastStep[]> results)
        {
            if (!seen.Add(current))
            {
                return;
            }

            foreach (var edge in Metadata.Edges.Where(edge => edge.FromIndex == current))
            {
                path.Add(new ActorStateUpcastStep(
                    ResolveNode(edge.FromIndex),
                    ResolveNode(edge.ToIndex),
                    hops[(edge.FromIndex, edge.ToIndex)]));
                if (edge.ToIndex == target)
                {
                    results.Add(path.ToArray());
                }
                else
                {
                    Visit(edge.ToIndex, target, seen, path, results);
                }

                path.RemoveAt(path.Count - 1);
            }

            seen.Remove(current);
        }
    }

    private sealed record ActorStateUpcastStep(
        ActorStateMigrationNode FromNode,
        ActorStateMigrationNode ToNode,
        ActorStateUpcastDelegate Upcast);
}

/// <summary>
/// Deserializes a closed enrolled actor state envelope.
/// </summary>
public delegate object? ActorStateDeserializeDelegate(ReadOnlyMemory<byte> payload, IActorStateMigrationSerializer serializer);

/// <summary>
/// Upcasts one closed actor state node to the next node.
/// </summary>
public delegate ValueTask<object> ActorStateUpcastDelegate(object state, CancellationToken cancellationToken);

/// <summary>
/// A closed deserializer for a migration node.
/// </summary>
public sealed record ActorStateNodeDeserializer(int ChainIndex, ActorStateDeserializeDelegate Deserialize);

/// <summary>
/// A closed hop delegate between two migration nodes.
/// </summary>
public sealed record ActorStateHopRegistration(int FromIndex, int ToIndex, ActorStateUpcastDelegate Upcast);

/// <summary>
/// Runtime registration data for one actor state migration family.
/// </summary>
public sealed record ActorStateMigrationFamilyRegistration(
    ActorStateMigrationFamily Metadata,
    IReadOnlyList<ActorStateNodeDeserializer> NodeDeserializers,
    IReadOnlyList<ActorStateHopRegistration> HopDelegates);
