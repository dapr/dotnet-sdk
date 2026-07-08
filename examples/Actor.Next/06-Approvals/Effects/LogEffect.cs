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
