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

using Dapr.Actors.Next.Abstractions.Exceptions;
using Grpc.Core;

namespace Dapr.Actors.Next.Streams;

/// <summary>
/// Default failure classifier used by the stream subscription runner.
/// </summary>
public sealed class DefaultActorStreamFailureClassifier : IActorStreamFailureClassifier
{
    /// <inheritdoc />
    public ActorStreamDeliveryAction Classify(Exception exception) =>
        exception switch
        {
            ActorStreamPoisonException => ActorStreamDeliveryAction.Drop,
            InvalidActorEventException => ActorStreamDeliveryAction.Drop,
            ArgumentException => ActorStreamDeliveryAction.Drop,
            RpcException { StatusCode: StatusCode.NotFound } => ActorStreamDeliveryAction.Drop,
            ActorStreamTransientException => ActorStreamDeliveryAction.Retry,
            TimeoutException => ActorStreamDeliveryAction.Retry,
            OperationCanceledException => ActorStreamDeliveryAction.Retry,
            _ => ActorStreamDeliveryAction.Retry,
        };
}
