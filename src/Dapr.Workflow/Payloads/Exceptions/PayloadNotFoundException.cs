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
/// Thrown when a payload reference cannot be resolved because the stored payload no longer exists.
/// This usually indicates that the store's retention period was shorter than the lifetime of the
/// workflow instance that references it.
/// </summary>
/// <param name="reference">The reference that could not be resolved.</param>
/// <param name="innerException">The underlying store exception, if any.</param>
public sealed class PayloadNotFoundException(PayloadReference reference, Exception? innerException = null) : Exception(
    $"The payload '{reference.Key}' in store '{reference.StoreName}' could not be found. " +
    "Ensure the store's retention exceeds the lifetime of the workflow instance that references it.", innerException)
{
    /// <summary>
    /// Gets the reference that could not be resolved.
    /// </summary>
    public PayloadReference Reference => reference;
}
