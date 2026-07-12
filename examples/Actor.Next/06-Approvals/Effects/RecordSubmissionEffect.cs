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

/// <summary>Copies the submitted document details from the event payload into the actor's state.</summary>
internal sealed class RecordSubmissionEffect : IActorEffect
{
    public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
    {
        var bag = ApprovalCapabilityRegistry.State(context);
        var submission = ApprovalCapabilityRegistry.Payload(context).Deserialize<SubmitDocument>()
                         ?? throw new InvalidOperationException("Submit payload is required.");

        bag.Set("requester", submission.Requester);
        bag.Set("amount", submission.Amount);
        bag.Set("parties", submission.Parties);
        bag.Set("simulateChargeFailure", submission.SimulateChargeFailure);
        return ValueTask.CompletedTask;
    }
}