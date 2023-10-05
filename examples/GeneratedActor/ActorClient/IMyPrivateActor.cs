using Dapr.Actors.Generators;

namespace GeneratedActor;

internal sealed record MyPrivateState(string Name);

/// <remarks>
/// Non-remoted invocations have a strict limit on a single argument; CancellationToken is not supported.
/// </remarks>
[GenerateActorClient]
internal interface IMyPrivateActor
{
    Task<MyPrivateState> GetStateAsync();

    Task SetStateAsync(MyPrivateState state);
}
