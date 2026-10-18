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

namespace Dapr.Workflow.Payloads;

/// <summary>
/// Thrown when a payload reference is encountered but no payload codec
/// capable of resolving it is registered. This typically means the application that produced the payload
/// had offloading enabled, but the consuming application does not.
/// </summary>
/// <param name="site">The boundary at which the reference was encountered.</param>
internal sealed class PayloadNotResolvableException(PayloadSite site) : Exception($"A payload reference was received at {site.Kind} for workflow instance '{site.InstanceId}' " +
    "but no payload converter capable of resolving references is registered. " +
    "Register payload support with WithBinaryStorePayloads. Cross-app inlining requires access to the stored payload.")
{
    /// <summary>
    /// Gets the bounary at which the reference was encountered.
    /// </summary>
    public PayloadSite Site => site;
}
