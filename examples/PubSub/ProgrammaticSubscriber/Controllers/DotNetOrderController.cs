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

namespace ProgrammaticSubscriber.Controllers
{
    [ApiController]
    public class DotNetOrderController : ControllerBase
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
        /// We can Also use TopicAttribute to tell sidecar the information about pubsub.
        /// In general way <see cref="OrderController"/>
        ///
        /// HTTPPost template should be same as the name of TopicAttribte
        /// </summary>
        /// <param name="data"></param>
        /// <param name="daprClient">used to other component like state.If you need use this,async way is better.</param>
        /// <returns></returns>
        [Topic("pubsub","order.update.dotnet")]
        [HttpPost("/order.update.dotnet")]
        public IActionResult OnOrderUpdateDotnet(PubSubEto<DemoOrderETO> raw,[FromServices] DaprClient daprClient)
        {
            var data = raw.data;
            Console.WriteLine(string.Format(reporter,data.OrderId,data.Type,data.OldValue,data.NewValue,data.DateTime));
            //to tell Dapr message ACK.
            return Ok();
        }   
     
        [Topic("pubsub","order.update")]
        [HttpPost("/order.update")]
        public IActionResult OnOrderUpdateGeneral(PubSubEto<DemoOrderETO> raw,[FromServices] DaprClient daprClient)
        {
            var data = raw.data;
            Console.WriteLine(string.Format(reporter,data.OrderId,data.Type,data.OldValue,data.NewValue,data.DateTime));
            //to tell Dapr message ACK.
            return Ok();
        }   
    }
}
