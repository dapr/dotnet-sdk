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

namespace Dapr.Client;

/// <summary>
/// Enum representing the response from a Unlock API call.
/// </summary>
public enum LockStatus
{
    /// <summary>
    /// Succes stating the lock is released.
    /// </summary>
    Success,
    /// <summary>
    /// LockDoesNotExist stating the lock does not exist.
    /// </summary>
    LockDoesNotExist,
    /// <summary>
    /// LockBelongsToOthers stating the lock is acquired by a different process.
    /// </summary>
    LockBelongsToOthers,
    /// <summary>
    /// InternalError statign an error in unlocking.
    /// </summary>
    InternalError,
}

/// <summary>
/// Class representing the response from a Unlock API call.
/// </summary>
public class UnlockResponse
{
    /// <summary>
    /// The status of unlock API call
    /// </summary>
    public LockStatus status { set; get; }

    /// <summary>
    /// Constructor for a UnlockResponse.
    /// </summary>
    /// <param name="status">The status value that is returned in the UnLock call.</param>
    public UnlockResponse(LockStatus status) {
        this.status = status;
    }
}