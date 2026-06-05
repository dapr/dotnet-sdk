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

namespace Dapr.StateManagement;

/// <summary>
/// Consistency mode for state operations with Dapr.
/// </summary>
public enum ConsistencyMode
{
    /// <summary>
    /// Eventual consistency: changes propagate asynchronously to all replicas.
    /// This provides lower latency but may return stale data.
    /// </summary>
    Eventual,

    /// <summary>
    /// Strong consistency: all replicas reflect the latest write before reads are served.
    /// This provides the most up-to-date data but may have higher latency.
    /// </summary>
    Strong,
}
