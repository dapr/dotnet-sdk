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

        /// <summary>
        /// this is the demo to show
        /// how to publish a custom business event.
        /// </summary>
        [HttpPost]
        public async void UpdateOrderAsync()
        {
            var payload = new DemoOrderETO()
            {
                OrderId = Guid.NewGuid(),
                DateTime = DateTime.Now,
                OldValue = "this is old value",
                NewValue = "this is new value"
            };
            Console.WriteLine(JsonSerializer.Serialize(payload));
            
            //Dapr can publish custom model by generic like under.
            await _daprClient.PublishEventAsync<DemoOrderETO>("pubsub", "order.update",payload);
        }
    }
}
