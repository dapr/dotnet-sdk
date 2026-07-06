// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

namespace Dapr.Actors.Communication;

using System;

/// <summary>
/// Represents a response from fetching an actor state key.
/// </summary>
public class ActorStateResponse<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActorStateResponse{T}"/> class.
    /// </summary>
    /// <param name="value">The response value.</param>
    /// <param name="ttlExpireTime">The time to live expiration time.</param>
    public ActorStateResponse(T value, DateTimeOffset? ttlExpireTime)
    {
        this.Value = value;
        this.TTLExpireTime = ttlExpireTime;
    }

    /// <summary>
    /// Gets the response value as a string.
    /// </summary>
    /// <value>
    /// The response value as a string.
    /// </value>
    public T Value { get; }

    /// <summary>
    /// Gets the time to live expiration time.
    /// </summary>
    /// <value>
    /// The time to live expiration time.
    /// </value>
    public DateTimeOffset? TTLExpireTime { get; }
}