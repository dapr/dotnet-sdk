
using System;
using Microsoft.Actions.Actors;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Microsoft.Actions.Actors.Client;
using IDemoActorIneterface;

namespace ActorClient
{
    class Program
    {
        static void Main(string[] args)
        {            
            var data = new MyData()
            {
                PropertyA = "ValueA",
                PropertyB = "ValueBCDEF",
            };           

            // Remoting Calls
            var proxyRemoting = ActorProxy.Create<IDemoActor>(new ActorId("abc"), "DemoActor");
            var echoResult = proxyRemoting.Echo(data).GetAwaiter().GetResult();
            //proxy.ProcessData(data).GetAwaiter().GetResult();
            
            
            
            // Invoke without Remoting, these invocations need method name.  NOT NEEDED FOR DEMO FOR REMOTING
            var proxy = ActorProxy.Create(new ActorId("abc"), "DemoActor");
            var resData = proxy.InvokeAsync<MyData>("Echo", data).GetAwaiter().GetResult();
        }
    }
}
