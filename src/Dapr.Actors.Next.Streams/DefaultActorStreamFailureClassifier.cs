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
