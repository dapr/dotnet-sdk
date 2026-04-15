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
/// A no-op proxy factory used for unit testing.
/// </summary>
internal sealed class NoOpVirtualActorProxyFactory : IVirtualActorProxyFactory
{
    /// <inheritdoc />
    public TActorInterface CreateProxy<TActorInterface>(VirtualActorId actorId, string? actorType = null)
        where TActorInterface : IVirtualActor =>
        throw new NotSupportedException("Actor proxy creation is not supported in the test context. Provide a mock IVirtualActorProxyFactory.");

    /// <inheritdoc />
    public IVirtualActorProxy CreateProxy(VirtualActorId actorId, string actorType) =>
        throw new NotSupportedException("Actor proxy creation is not supported in the test context. Provide a mock IVirtualActorProxyFactory.");
}
