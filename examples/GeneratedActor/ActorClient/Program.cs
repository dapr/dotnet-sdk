using Dapr.Actors;
using Dapr.Actors.Client;
using GeneratedActor;

Console.WriteLine("Testing generated client...");

var proxy = ActorProxy.Create(ActorId.CreateRandom(), "RemoteActor");

using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(30));

var client = new ClientActorClient(proxy);

var state = await client.GetStateAsync(cancellationTokenSource.Token);

await client.SetStateAsync(new ClientState("Hello, World!"), cancellationTokenSource.Token);

Console.WriteLine("Done!");
