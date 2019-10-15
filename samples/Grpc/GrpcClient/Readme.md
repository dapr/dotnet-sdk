# Dapr Grpc Client Sample
The gRPC client sample shows how to make Dapr calls to publish events, save state, get state and delete state using a gRPC client. 

## Prerequistes
* [.Net Core SDK 3.0](https://dotnet.microsoft.com/download)
* [Dapr CLI](https://github.com/dapr/cli)
* [Dapr DotNet SDK](https://github.com/dapr/dotnet-sdk)


 ## Running the Sample

 To run the sample locally run this command in GrpcClient directory:
 ```sh
 dapr run --app-id gRPC_Client dotnet run
 ```

 Above command will run dapr runtime and launch the app and will show logs both form Dapr runtime and the application. The client app will make calls to dapr runtime to publish events, save state, get state and delete state using the gRPC client.
 Logs form application will show following in the command window:
```sh
  Published Event!
  Saved State!
  Got State: my data
  Deleted State!
 ```

 ### Making Client calls.
 Actor Client project shows how to make client calls for actor using Remoting which provides a strongly typed invocation experience.
 Run the client project from ActorClient directory as:
```sh
 dotnet run
 ```

 ### Invoke Actor method without Remoting over Http.
You can invoke Actor methods without remoting (directly over http), if Actor method accepts at-most one argument.
Actor runtime will deserialize the incoming request body from client and use it as method argument to invoke the actor method.
When making non-remoting calls Actor method arguments and return types are serialized, deserialized as json.


**Save Data**
Following curl call will save data for actor id "abc"

 ```sh
curl -X POST http://localhost:3500/v1.0/actors/DemoActor/abc/method/SaveData -d '{ "PropertyA": "ValueA", "ProertyB": "ValueB" }'
 ```

**Get Data**
Following curl call will get data for actor id "abc"

 ```sh
curl -X POST http://localhost:3500/v1.0/actors/DemoActor/abc/method/GetData -d '{ "PropertyA": "ValueA", "ProertyB": "ValueB" }'
 ```