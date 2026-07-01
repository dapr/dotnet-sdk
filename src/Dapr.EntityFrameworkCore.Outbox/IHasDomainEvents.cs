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

namespace Dapr.EntityFrameworkCore.Outbox;

/// <summary>
/// Marker interface implemented by aggregate roots that raise domain events.
/// The <c>DaprOutboxSaveChangesInterceptor</c> discovers implementations of
/// this interface on tracked entities, converts each pending event into an
/// <see cref="OutboxMessage"/> row inside the same <c>SaveChangesAsync</c>
/// transaction, and then calls <see cref="ClearDomainEvents"/>.
/// </summary>
public interface IHasDomainEvents
{
    /// <summary>
    /// The domain events pending publication for this aggregate.
    /// Returning an empty collection means no events will be enqueued.
    /// </summary>
    IReadOnlyCollection<object> DomainEvents { get; }

    /// <summary>
    /// Clears the pending domain events after they have been captured into the outbox.
    /// The interceptor calls this method after successfully enqueuing all events, so
    /// implementations should reset their internal collection here.
    /// </summary>
    void ClearDomainEvents();
}
