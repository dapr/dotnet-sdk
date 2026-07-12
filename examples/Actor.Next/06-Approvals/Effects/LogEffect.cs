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

using Dapr.Actors.Next.Abstractions.Filters;

namespace Approvals.Next.Example06.Effects;

/// <summary>A side-effect-free effect that just logs; stands in for a real notification.</summary>
internal sealed partial class LogEffect(ILogger logger, string message) : IActorEffect
{
    public ValueTask ExecuteAsync(ActorCapabilityContext context, CancellationToken cancellationToken = default)
    {
        LogDocumentEffect(context.ActorId.Value, message);
        return ValueTask.CompletedTask;
    }

    [LoggerMessage(LogLevel.Information, "Document {DocumentId}: {Message}")]
    private partial void LogDocumentEffect(string documentId, string message);
}
