using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using PubSub.Domain;

namespace Publisher.Controllers
{
    [ApiController]
    [Route("Order")]
    public class OrderController : ControllerBase
    {
        private readonly DaprClient _daprClient;

        public OrderController(DaprClient daprClient)
        {
            _daprClient = daprClient;
        }

        private readonly Guid id = Guid.NewGuid();
        private static Order order=null; 
        /// <summary>
        /// this is the demo to show
        /// how to publish a custom business event.
        /// </summary>
        [HttpPost]
        public async void UpdateOrderOwnerAsync(Guid newOwnerId)
        {
            order ??= new Order() { OrderId = id, OwnerId = Guid.NewGuid() };
            var oldValue = order.OwnerId;
            order.OwnerId = newOwnerId;

            var payload = new DemoOrderETO()
            {
                OrderId = order.OrderId,
                DateTime = DateTime.Now,
                OldValue = oldValue.ToString(),
                NewValue = newOwnerId.ToString(),
                Type = nameof(order.OwnerId)
            };
            Console.WriteLine(JsonSerializer.Serialize(payload));
            
            //Dapr can publish custom model by generic like under.
            await _daprClient.PublishEventAsync<DemoOrderETO>("pubsub", "order.update",payload);
        }
    }
}
