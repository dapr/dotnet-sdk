using System;

namespace PubSub.Domain
{
    public class Order
    {
        public Guid OrderId { get; set; }
        public Guid OwnerId { get; set; }
    }
}
