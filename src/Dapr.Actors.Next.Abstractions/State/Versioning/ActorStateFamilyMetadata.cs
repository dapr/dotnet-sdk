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
/// Describes a connected actor state migration family.
/// </summary>
/// <param name="CanonicalName">The canonical family name.</param>
/// <param name="Nodes">The ordered state nodes in the family.</param>
/// <param name="Edges">The directed upcaster edges in the family.</param>
public sealed record ActorStateMigrationFamily(
    string CanonicalName,
    IReadOnlyList<ActorStateMigrationNode> Nodes,
    IReadOnlyList<ActorStateMigrationEdge> Edges);

/// <summary>
/// Describes a state shape in a migration chain.
/// </summary>
/// <param name="Index">The node position in the family chain.</param>
/// <param name="ClrType">The CLR type represented by the node.</param>
/// <param name="ShapeHash">The algorithm-versioned structural hash for the node.</param>
public sealed record ActorStateMigrationNode(int Index, Type ClrType, string ShapeHash);

/// <summary>
/// Describes a directed migration edge between two state nodes.
/// </summary>
/// <param name="FromIndex">The source node index.</param>
/// <param name="ToIndex">The target node index.</param>
/// <param name="UpcasterType">The CLR type that implements the upcaster, when hand-authored.</param>
public sealed record ActorStateMigrationEdge(int FromIndex, int ToIndex, Type? UpcasterType = null);
