namespace Dapr.Actors.Next.Core.Runtime;

internal sealed class ActorTurnExecution(object key, IReadOnlyDictionary<string, string> headers)
{
    private static readonly AsyncLocal<ActorTurnExecution?> CurrentExecution = new();

    public static ActorTurnExecution? Current => CurrentExecution.Value;

    public object Key { get; } = key;

    public IReadOnlyDictionary<string, string> Headers { get; } = headers;

    public int ReentrantDepth { get; set; }

    public static IDisposable Push(ActorTurnExecution execution)
    {
        var prior = CurrentExecution.Value;
        CurrentExecution.Value = execution;
        return new PopWhenDisposed(prior);
    }

    private sealed class PopWhenDisposed(ActorTurnExecution? prior) : IDisposable
    {
        public void Dispose()
        {
            CurrentExecution.Value = prior;
        }
    }
}
