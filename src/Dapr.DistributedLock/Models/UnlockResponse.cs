namespace Dapr.DistributedLock.Models;

/// <summary>
/// Represents the response from an attempt to unlock a distributed lock.
/// </summary>
/// <param name="Status">The status of the lock following the attempted unlock operation.</param>
public sealed record UnlockResponse(LockStatus Status);
