using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PubSub.Domain;

namespace Subscriber.Controllers
{
    [ApiController]
    [Route("Order")]
    public class Order : ControllerBase
    {
        private readonly string reporter = "====================\r\n" +
                                           "OrderId:\t\t{0}\r\n" +
                                           "OldValue:\t\t{1}\r\n" +
                                           "NewValue:\t\t{2}\r\n" +
                                           "OrderUpdateTime:\t{3}\r\n" +
                                           "====================";
        [HttpPost]
        public async Task<OkResult> OnOrderUpdateAsync(PubSubEto<DemoOrderETO> meta)
        {
            return await Task.Run(() =>
            {
                Console.WriteLine(string.Format(reporter,meta.data.OrderId,meta.data.OldValue,meta.data.NewValue,meta.data.DateTime));
                
                //to tell Dapr message ACK.
                return Ok();
            });
        }   
    }
}
