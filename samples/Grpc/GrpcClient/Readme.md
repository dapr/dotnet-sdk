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

 Above command will run Dapr runtime and launch the app and will show logs both form Dapr runtime and the application. The client app will make calls to Dapr runtime to publish events, save state, get state and delete state using the gRPC client.
 Logs form application will show following in the command window:
```sh
  Published Event!
  Saved State!
  Got State: my data
  Deleted State!
 ```
