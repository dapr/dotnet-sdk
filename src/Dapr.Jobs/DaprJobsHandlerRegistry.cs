// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

namespace Dapr.Jobs;

/// <summary>
/// Holds the registered job handler delegate and optional timeout for use by
/// the gRPC <see cref="DaprJobsAppCallbackService"/> callback service.
/// </summary>
internal sealed class DaprJobsHandlerRegistry
{
    /// <summary>
    /// The delegate provided by the developer that handles inbound job trigger invocations.
    /// </summary>
    public Delegate? Handler { get; set; }

    /// <summary>
    /// Optional per-request timeout applied to the cancellation token supplied to the handler.
    /// </summary>
    public TimeSpan? Timeout { get; set; }
}
