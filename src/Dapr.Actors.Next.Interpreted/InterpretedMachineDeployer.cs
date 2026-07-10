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

namespace Dapr.Actors.Next.Interpreted;

/// <summary>
/// Applies the generate, verify, deploy guard for interpreted machine definitions.
/// </summary>
public sealed class InterpretedMachineDeployer(IInterpretedMachineVerifier verifier, IInterpretedMachineStore store)
{
    /// <summary>
    /// Verifies and stores a machine definition for an interpreted actor instance.
    /// </summary>
    public async ValueTask DeployAsync(
        string actorType,
        ActorId actorId,
        InterpretedMachineDefinition definition,
        CancellationToken cancellationToken = default)
    {
        verifier.Verify(definition).ThrowIfInvalid();
        await store.SetAsync(actorType, actorId, definition, cancellationToken).ConfigureAwait(false);
    }
}
