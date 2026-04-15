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

namespace Dapr.VirtualActors;

/// <summary>
/// Represents the metadata about a registered actor type.
/// </summary>
/// <remarks>
/// This record captures the relationship between an actor implementation type,
/// its declared actor interfaces, and the type name used for Dapr actor identification.
/// </remarks>
/// <param name="ActorTypeName">
/// The name used to identify this actor type in Dapr. This is the name used in
/// actor method invocations and state store operations.
/// </param>
/// <param name="ImplementationType">
/// The CLR type of the actor implementation class.
/// </param>
/// <param name="InterfaceTypes">
/// The actor interfaces implemented by this actor type.
/// </param>
public sealed record ActorTypeInformation(
    string ActorTypeName,
    Type ImplementationType,
    IReadOnlyList<Type> InterfaceTypes);
