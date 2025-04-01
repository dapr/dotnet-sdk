// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Dapr.Client;

/// <summary>
/// Class representing the response from a Lock API call.
/// </summary>
[Obsolete("This API is currently not stable as it is in the Alpha stage. This attribute will be removed once it is stable.")]
public sealed class TryLockResponse :  IAsyncDisposable
{
    /// <summary>
    /// The success value of the tryLock API call
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The resourceId required to unlock the lock
    /// </summary>
    public string ResourceId { get; init; }

    /// <summary>
    /// The LockOwner required to unlock the lock
    /// </summary>
    public string LockOwner { get; init; }

    /// <summary>
    /// The StoreName required to unlock the lock
    /// </summary>
    public string StoreName { get; init; }

    /// <summary>
    /// Constructor for a TryLockResponse.
    /// </summary>
    public TryLockResponse()
    {
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        using var client = new DaprClientBuilder().Build();
        if(this.Success) {
            await client.Unlock(StoreName, ResourceId, LockOwner);
        }
    }
}
