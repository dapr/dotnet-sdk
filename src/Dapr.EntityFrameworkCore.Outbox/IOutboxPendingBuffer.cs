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

using Microsoft.EntityFrameworkCore;

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Singleton buffer that associates explicit outbox enqueues with the specific
/// <see cref="DbContext"/> instance that produced them, so the interceptor drains
/// the right pending list on <c>SaveChangesAsync</c> even when multiple contexts
/// coexist in a scope. Advanced scenarios may substitute a custom implementation
/// on the service collection.
/// </summary>
public interface IOutboxPendingBuffer
{
    /// <summary>Records a pending enqueue for the given <see cref="DbContext"/>.</summary>
    void Enqueue(DbContext context, PendingEnqueue pending);

    /// <summary>
    /// Removes and returns all pending enqueues recorded for the given <see cref="DbContext"/>.
    /// Returns an empty array when no enqueues are pending.
    /// </summary>
    IReadOnlyList<PendingEnqueue> Drain(DbContext context);
}
