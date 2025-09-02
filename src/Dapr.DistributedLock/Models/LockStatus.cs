namespace Dapr.DistributedLock.Models;

/// <summary>
/// Represents the result of an attempt to unlock an existing lock.
/// </summary>
public enum LockStatus
{
    /// <summary>
    /// Indicates the lock was released successfully.
    /// </summary>
    Success,
    /// <summary>
    /// Indicates the lock does not exist.
    /// </summary>
    LockDoesNotExist,
    /// <summary>
    /// Indicates the lock was acquired by another process.
    /// </summary>
    LockBelongsToOthers,
    /// <summary>
    /// Indicates there was an error while unlocking.
    /// </summary>
    InternalError
}
