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

using System.Text.Json;
using Dapr.Actors.Next.Abstractions.Filters;
using Dapr.Actors.Next.Examples.Approvals;

namespace Approvals.Next.Example06.Effects;

/// <summary>Records the approver's decision on the document.</summary>
internal sealed class RecordDecisionEffect : IActorEffect
{
    public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
    {
        var bag = ApprovalCapabilityRegistry.State(context);
        var decision = ApprovalCapabilityRegistry.Payload(context).Deserialize<Decision>();
        if (decision is not null)
        {
            bag.Set("approver", decision.Approver);
            bag.Set("decisionNote", decision.Note ?? string.Empty);
        }

        return ValueTask.CompletedTask;
    }
}
