// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors.State;

/// <summary>
/// Actor interface that exposes basic key–value state operations with optional TTL support.
/// </summary>
public interface IStateActor : IPingActor, IActor
{
    /// <summary>
    /// Returns the value associated with <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The state key to retrieve.</param>
    Task<string> GetState(string key);

    /// <summary>
    /// Sets or overwrites the value for <paramref name="key"/>, optionally with a TTL.
    /// </summary>
    /// <param name="key">The state key to set.</param>
    /// <param name="value">The value to store.</param>
    /// <param name="ttl">Optional time-to-live after which the entry expires.</param>
    Task SetState(string key, string value, TimeSpan? ttl);
}
