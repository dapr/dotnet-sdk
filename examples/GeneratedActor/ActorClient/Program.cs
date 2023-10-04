using Dapr.Actors;
using Dapr.Actors.Client;
using GeneratedActor;

var actorId = new ActorId("1");
var actorType = "MyPublicActor";

try
{
    //await TestRemotedActorAsync();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

try
{
    await TestNonRemotedActorAsync();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

try
{
    await TestNonRemotedManualProxyAsync();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

Console.WriteLine("Hello, World!");

/*
async Task TestRemotedActorAsync()
{
    Console.WriteLine("Testing remoted actor client...");

    var client = ActorProxy.Create<IMyPublicActor>(actorId, actorType);

    var state = await client.GetStateAsync();

    await client.SetStateAsync(new MyState("Hello, World!"));
}
*/

async Task TestNonRemotedActorAsync()
{
    Console.WriteLine("Testing non-remoted actor client...");

    var client = ActorProxy.Create(actorId, actorType /*, new ActorProxyOptions { UseJsonSerialization = true } */);

    var state = await client.InvokeMethodAsync<MyState>("GetStateAsync");

    await client.InvokeMethodAsync("SetStateAsync", new MyState("Hello, World!"));
}

async Task TestNonRemotedManualProxyAsync()
{
    Console.WriteLine("Testing non-remoted manual proxy...");

    var proxy = ActorProxy.Create(actorId, actorType /*, new ActorProxyOptions { UseJsonSerialization = true } */);

    var client = new MyPublicActorManualProxy(proxy);

    var state = await client.GetStateAsync();

    await client.SetStateAsync(new MyState("Hello, World!"));
}
