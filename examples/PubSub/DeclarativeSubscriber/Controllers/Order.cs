using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PubSub.Domain;

namespace DeclarativeSubscriber.Controllers
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
        
        [HttpPost]
        public IActionResult OnOrderUpdate(PubSubEto<DemoOrderETO> meta)
        {
            Console.WriteLine(string.Format(reporter,meta.data.OrderId,meta.data.OldValue,meta.data.NewValue,meta.data.DateTime));
            
            //to tell Dapr message ACK.
            return Ok();
        }   
    }
}
