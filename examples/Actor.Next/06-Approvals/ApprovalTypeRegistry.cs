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

using Dapr.Actors.Next.Abstractions.Registry;
using Dapr.Actors.Next.Interpreted;

namespace Dapr.Actors.Next.Examples.Approvals;

/// <summary>
/// Describes the single compiled interpreted actor type this app hosts and the one method it exposes,
/// so a generic caller can discover it without a compiled document contract.
/// </summary>
public sealed class ApprovalTypeRegistry : IActorRegistry
{
    private readonly ActorTypeDescriptor _descriptor = new(
        ApprovalDefinitions.ActorType,
        1,
        typeof(InterpretedStateMachineActor),
        typeof(InterpretedStateMachineActor),
        [
            new ActorMethodDescriptor(
                "Raise",
                "Raise",
                typeof(InterpretedRaiseResult),
                [new ActorParameterDescriptor("evt", typeof(InterpretedEvent), 0, false, false, null)]),
        ]);

    public IReadOnlyList<ActorTypeDescriptor> Actors => [_descriptor];

    public bool TryGet(string actorType, out ActorTypeDescriptor value)
    {
        if (string.Equals(actorType, ApprovalDefinitions.ActorType, StringComparison.Ordinal))
        {
            value = _descriptor;
            return true;
        }

        value = null!;
        return false;
    }
}
