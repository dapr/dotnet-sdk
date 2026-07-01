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

using System.Collections.ObjectModel;
using Dapr.EntityFrameworkCore.Outbox;

namespace Samples.Outbox;

/// <summary>
/// A tiny domain aggregate that raises a single event when created.
/// Illustrates the <see cref="IHasDomainEvents"/> pattern the outbox interceptor uses.
/// </summary>
public sealed class Order : IHasDomainEvents
{
    private readonly List<object> domainEvents = new();

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string CustomerName { get; private set; } = string.Empty;
    public decimal TotalAmount { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public IReadOnlyCollection<object> DomainEvents => new ReadOnlyCollection<object>(domainEvents);

    public void ClearDomainEvents() => domainEvents.Clear();

    public static Order Create(string customerName, decimal totalAmount)
    {
        var order = new Order
        {
            CustomerName = customerName,
            TotalAmount = totalAmount,
        };
        order.domainEvents.Add(new OrderPlaced(order.Id, order.CustomerName, order.TotalAmount, order.CreatedAt));
        return order;
    }
}

/// <summary>
/// Event serialized to the outbox as JSON and published by Dapr as a CloudEvent
/// on the <c>orders</c> topic of the <c>pubsub</c> component.
/// </summary>
[DaprOutboxEvent("pubsub", "orders", CloudEventType = "com.dapr.samples.order.placed")]
public sealed record OrderPlaced(Guid OrderId, string CustomerName, decimal TotalAmount, DateTimeOffset OccurredAt);
