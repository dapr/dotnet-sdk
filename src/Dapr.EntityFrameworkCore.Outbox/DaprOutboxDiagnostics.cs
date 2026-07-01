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

using System.Diagnostics;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Shared diagnostic primitives (activity source, event ids) for the Dapr EF Core outbox.
/// Consumers wire up <see cref="ActivitySource"/> in their OpenTelemetry configuration by
/// name; the SDK does not take a dependency on OpenTelemetry.
/// </summary>
internal static class DaprOutboxDiagnostics
{
    /// <summary>
    /// The activity source name used for all outbox spans.
    /// </summary>
    public const string ActivitySourceName = "Dapr.EntityFrameworkCore.Outbox";

    /// <summary>
    /// The activity source used by the interceptor and dispatcher.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
