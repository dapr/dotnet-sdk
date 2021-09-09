using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using PubSub.Domain;

namespace ProgrammaticSubscriber.Controllers
{
    [ApiController]
    [Route("Order")]
    public class OrderController : ControllerBase
    {
        private readonly string reporter = 
            @"====================
OrderId:           {0}
Type:              {1}
OldValue:          {2}
NewValue:          {3}
OrderUpdateTime:   {4}
====================";
        
        
        /// <summary>
        /// this is the universal way to tell sidecar which I need sub.
        /// It can achieve by other language like Node, python, Go etc.
        /// In this way,this project can be built without "Dapr.AspNetCore" nuget package.
        ///
        /// about Dotnet way <see cref="DotNetOrderController"/>
        ///
        /// only choose one way. 
        /// </summary>
        /// <returns></returns>
        // [HttpGet]
        // [Route("/dapr/subscribe")]
        // public IActionResult DaprSubscribe()
        // {
        //     var data =new {pubsubname = "pubsub",topic="order.update",route="/Order"};
        //     return new JsonResult(new ArrayList(){data});
        // }
        
        
        [HttpPost]
        public IActionResult OnOrderUpdate(PubSubEto<DemoOrderETO> meta)
        {
            // Console.WriteLine(JsonSerializer.Serialize(meta));
            Console.WriteLine(string.Format(reporter,meta.data.OrderId,meta.data.Type,meta.data.OldValue,meta.data.NewValue,meta.data.DateTime));
            
            //to tell Dapr message ACK.
            return Ok();
        }
    }
}
