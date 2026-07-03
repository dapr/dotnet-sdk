using Dapr.Actors.Next.Core.Runtime;

namespace Dapr.Actors.Next.Core.Scheduling;

internal sealed class ActorTurnWork
{
    private readonly TaskCompletionSource<byte[]?> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration cancellationRegistration;

    public ActorTurnWork(ActorRuntimeRequest request, CancellationToken cancellationToken)
    {
        Request = request;
        CancellationToken = cancellationToken;
        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(
                static state =>
                {
                    var work = (ActorTurnWork)state!;
                    work.completion.TrySetCanceled(work.CancellationToken);
                },
                this);
        }
    }

    public ActorRuntimeRequest Request { get; }

    public CancellationToken CancellationToken { get; }

    public Task<byte[]?> Task => completion.Task;

    public void Complete(byte[]? value)
    {
        cancellationRegistration.Dispose();
        completion.TrySetResult(value);
    }

    public void Fail(Exception exception)
    {
        cancellationRegistration.Dispose();
        completion.TrySetException(exception);
    }
}
