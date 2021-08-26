using System;

namespace PubSub.Domain
{
    public class PubSubEto<T>
    {
        public T data { get; set; }
        
        public Guid id { get; set; }
        
        public string type { get; set; }
        public string topic { get; set; }
        public string source { get; set; }
        public string traceid { get; set; }
        public string pubsubname { get; set; }
        public string specversion { get; set; }
        public string datacontenttype { get; set; }
    }
}
