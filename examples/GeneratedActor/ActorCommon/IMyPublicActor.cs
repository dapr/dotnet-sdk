using Dapr.Actors;

namespace GeneratedActor;

public sealed record MyState(string Name);

/// <summary>
/// 
/// </summary>
/// <remarks>
/// Non-remoted invocations have a strict limit on a single argument; CancellationToken is not supported.
/// </remarks>
public interface IMyPublicActor : IActor
{
    Task<MyState> GetStateAsync();

    Task SetStateAsync(MyState state);
}
