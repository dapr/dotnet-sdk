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

using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors;

namespace Dapr.IntegrationTest.Actors;

/// <summary>
/// Minimal actor interface used as a readiness probe.
/// </summary>
public interface IPingActor : IActor
{
    /// <summary>
    /// Pings the actor to verify that the runtime is available.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that cancels the underlying HTTP request, allowing the caller to impose a
    /// per-attempt timeout instead of waiting for the HttpClient default (100 s).
    /// </param>
    Task Ping(CancellationToken cancellationToken = default);
}
