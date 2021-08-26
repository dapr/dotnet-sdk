using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Dapr;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using PubSub.Domain;

namespace ProgrammaticSubscriberDotNet.Controllers
{
    [ApiController]
    [Route("Order")]
    public class Order : ControllerBase
    {
        private readonly string reporter = 
@"====================
OrderId:           {0}
Type:              {1}
OldValue:          {2}
NewValue:          {3}
OrderUpdateTime:   {4}
====================";
        
        [Topic("pubsub","order.update")]
        [HttpPost("order.update")]
        public async Task<OkResult> OnOrderUpdateAsync(DemoOrderETO data,[FromServices] DaprClient daprClient)
        {
            return await Task.Run(() =>
            {
                Console.WriteLine(string.Format(reporter,data.OrderId,data.Type,data.OldValue,data.NewValue,data.DateTime));
                
                //to tell Dapr message ACK.
                return Ok();
            });
        }   
    }
}
