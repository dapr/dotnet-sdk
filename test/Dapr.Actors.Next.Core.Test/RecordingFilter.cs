using Dapr.Actors.Next.Abstractions.Filters;

namespace Dapr.Actors.Next.Core.Test;

public sealed class RecordingFilter : IActorTurnFilter
{
    private readonly List<string> events;

    public RecordingFilter(List<string> events)
    {
        this.events = events;
    }

    public async ValueTask InvokeAsync(ActorTurnContext context, ActorTurnDelegate next)
    {
        events.Add("filter-before");
        await next(context);
        events.Add("filter-after");
    }
}
