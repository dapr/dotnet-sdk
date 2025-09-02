// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Diagnostics.CodeAnalysis;
using Dapr.Common;
using Dapr.DistributedLock.Models;

namespace Dapr.DistributedLock;

/// <summary>
/// Provides the implementation shape for the Dapr distributed lock client.
/// </summary>
[Experimental("DAPR_DISTRIBUTEDLOCK", UrlFormat = "https://docs.dapr.io/developing-applications/building-blocks/distributed-lock/distributed-lock-api-overview/")]
public interface IDaprDistributedLockClient : IDaprClient
{
    /// <summary>
    /// Attempt to lock the given resourceId with response indicating success.
    /// </summary>
    /// <param name="storeName">The name of the lock store to be queried.</param>
    /// <param name="resourceId">Lock key that stands for which resource to protect.</param>
    /// <param name="lockOwner">Indicates the identifier of lock owner.</param>
    /// <param name="expiryInSeconds">The time after which the lock gets expired.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> containing a <see cref="LockResponse"/></returns>
    public Task<LockResponse> TryLockAsync(string storeName, string resourceId, string lockOwner, int expiryInSeconds,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Attempt to unlock the given resourceId with response indicating success. 
    /// </summary>
    /// <param name="storeName">The name of the lock store to be queried.</param>
    /// <param name="resourceId">Lock key that stands for which resource to protect.</param>
    /// <param name="lockOwner">Indicates the identifier of lock owner.</param>
    /// <param name="cancellationToken">A <see cref="CancellationToken" /> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Task"/> containing a <see cref="UnlockResponse"/></returns>
    [Experimental("DAPR_DISTRIBUTEDLOCK", UrlFormat = "https://docs.dapr.io/developing-applications/building-blocks/distributed-lock/distributed-lock-api-overview/")]
    public Task<UnlockResponse> TryUnlockAsync(
        string storeName,
        string resourceId,
        string lockOwner,
        CancellationToken cancellationToken = default);
}
