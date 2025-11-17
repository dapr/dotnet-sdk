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

namespace Dapr.DistributedLock.Models;

/// <summary>
/// Class representing the response from a Lock API call.
/// </summary>
[Experimental("DAPR_DISTRIBUTEDLOCK", UrlFormat = "https://docs.dapr.io/developing-applications/building-blocks/distributed-lock/distributed-lock-api-overview/")]
public sealed class LockResponse : IAsyncDisposable
{
    private readonly IDaprDistributedLockClient _daprClient;
    
    /// <summary>
    /// Constructs a new instance of a <see cref="LockResponse"/>.
    /// </summary>
    internal LockResponse(IDaprDistributedLockClient daprClient)
    {
        _daprClient = daprClient;
    }
    
    /// <summary>
    /// The resource identifier that was locked.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// The name of the owner required to unlock the lock.
    /// </summary>
    public required string LockOwner { get; init; }

    /// <summary>
    /// The name of the store required to unlock the lock.
    /// </summary>
    public required string StoreName { get; init; }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _daprClient.TryUnlockAsync(StoreName, ResourceId, LockOwner).ConfigureAwait(false);
    }
}
