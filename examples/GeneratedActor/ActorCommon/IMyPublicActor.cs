using System.Reflection.Metadata.Ecma335;
using Dapr.Actors;
using Dapr.Actors.Generators;

namespace GeneratedActor;

public sealed record MyState(string Name);

/// <remarks>
/// Non-remoted invocations have a strict limit on a single argument; CancellationToken is not supported.
/// </remarks>
[GenerateActorClient(Name = "MyBestActorClient")]
public interface IMyPublicActor : IActor
{
    Task<MyState> GetStateAsync();

    Task SetStateAsync(MyState state);
}
