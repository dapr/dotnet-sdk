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
/// Thrown when a retrieved payload does not match the <see cref="PayloadReference.ContentHash"/>
/// recorded in its reference.
/// </summary>
public sealed class PayloadIntegrityException(PayloadReference reference, string actualHash) : Exception(
    $"The payload '{reference.Key}' in store '{reference.StoreName}' failed integrity verification. " +
    "Expected SHA-256 {reference.ContentHash} but computed {actualHash}")
{
    /// <summary>
    /// Gets the reference whose payload failed verification.
    /// </summary>
    public PayloadReference Reference => reference;

    /// <summary>
    /// Gets the hash computed from the retrieved bytes.
    /// </summary>
    public string ActualHash => actualHash;
}
