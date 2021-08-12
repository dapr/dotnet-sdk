using System;

namespace PubSub.Domain
{
    public class DemoOrderETO
    {
        public Guid OrderId { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public DateTime DateTime { get; set; }
    }
}
