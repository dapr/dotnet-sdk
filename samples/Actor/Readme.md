# Dapr Actor Sample
The Actor sample shows how to create an Actor(`DemoActor`) and invoke its methods on the client application. 

## Prerequistes
* [.Net Core SDK 3.0](https://dotnet.microsoft.com/download)
* [Dapr CLI](https://github.com/dapr/cli)
* [Dapr DotNet SDK](https://github.com/dapr/dotnet-sdk)


## Projects in sample
You can open the solution file code.sln in repo root to load the samples and product code.

* **The interface project(\IDemoActor).** This project contains the interface definition for the actor. The interface defines the actor contract that is shared by the actor implementation and the clients calling the actor. Because client projects may depend on it, it typically makes sense to define it in an assembly that is separate from the actor implementation.

* **The actor service project(\DemoService).** This project implements ASP.Net Core web service that is going to host the actor. It contains the implementation of the actor. An actor implementation is a class that derives from the base type Actor and implements the interfaces defined in the MyActor.Interfaces project. An actor class must also implement a constructor that accepts an ActorService instance and an ActorId and passes them to the base Actor class.

* **The actor client project(\ActorClient)** This project contains the implementation of the actor client which calls DemoActor's method defined in Actor Interfaces.

```
Actor --- IDemoActor
         |
         +- DemoActor
         |
         +- ActorClient
```


 ## Running the Sample

 To run the actor service locally run this comment in DemoActor directory:
 ```sh
 dapr run --port 3500 --app-id demo_actor --app-port 5001 dotnet run
 ```

 The actor service will listen on port 5001 for HTTP.

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
(below calls on MacOs, Linux & Windows are exactly the same except for escaping quotes on Windows for curl)

On Linux, MacOS:
 ```sh
curl -X POST http://localhost:3500/v1.0/actors/DemoActor/abc/method/SaveData -d '{ "PropertyA": "ValueA", "PropertyB": "ValueB" }'
 ```
 On Windows:
 ```sh
curl -X POST http://localhost:3500/v1.0/actors/DemoActor/abc/method/SaveData -d "{ \"PropertyA\": \"ValueA\", \"PropertyB\": \"ValueB\" }"
 ```
 
 

**Get Data**
Following curl call will get data for actor id "abc"
(below calls on MacOs, Linux & Windows are exactly the same except for escaping quotes on Windows for curl)
On Linux, MacOS:
 ```sh
curl -X POST http://localhost:3500/v1.0/actors/DemoActor/abc/method/GetData
 ```
 
 On Windows:
 ```sh
curl -X POST http://localhost:3500/v1.0/actors/DemoActor/abc/method/GetData
 ```
