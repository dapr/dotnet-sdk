// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Common;

/// <summary>
/// Describes whether the connected Dapr runtime exposes a gRPC capability.
/// </summary>
internal enum DaprRuntimeSupport
{
    /// <summary>
    /// Runtime discovery could not determine whether the capability is available.
    /// </summary>
    Unknown,

    /// <summary>
    /// Runtime discovery confirmed that the capability is unavailable.
    /// </summary>
    Unsupported,

    /// <summary>
    /// Runtime discovery confirmed that the capability is available.
    /// </summary>
    Supported,
}
