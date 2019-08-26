
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


            // Invoke without Remoting, cross language invocation.
             var proxy = ActorProxy.Create(new ActorId("abc"), "DemoActor");
            var resData = proxy.InvokeAsync<MyData>("Echo", data).GetAwaiter().GetResult();
            proxy.InvokeAsync("RegisterTimer").GetAwaiter().GetResult();                                  
            proxy.InvokeAsync("UnregisterReminder").GetAwaiter().GetResult();
            proxy.InvokeAsync("NoReturnTypeNoArg").GetAwaiter().GetResult();
            proxy.InvokeAsync<string>("DoesntExist", data).GetAwaiter().GetResult();
            try
            {
                proxy.InvokeAsync<string>("ThrowException", data).GetAwaiter().GetResult();
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex);
            }
            
             var result = proxy.InvokeAsync<string>("ProcessData", data).GetAwaiter().GetResult();
             //var resData = proxy.InvokeAsync<MyData>("GetData", null).GetAwaiter().GetResult();


            // Remoting Calls
            var proxyRemoting = ActorProxy.Create<IDemoActor>(new ActorId("abc"), "DemoActor");
            var echoResult = proxyRemoting.Echo(data).GetAwaiter().GetResult();
            //proxy.ProcessData(data).GetAwaiter().GetResult();
        }
    }
}
