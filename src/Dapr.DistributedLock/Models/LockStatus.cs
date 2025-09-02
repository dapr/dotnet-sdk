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
