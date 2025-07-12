# Dapr Actor example

The Actor example shows how to create a virtual actor (`DemoActor`) and invoke its methods on the client application.

## Prerequisites

- [.NET 8+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)

## Projects in sample

- The **interface project (`\IDemoActor`)** contains the interface definition for the actor. The interface defines the actor contract that is shared by the actor implementation and the clients calling the actor. Because client projects may depend on it, it typically makes sense to define it in an assembly that is separate from the actor implementation.

- The **actor service project (`\DemoActor`)** implements ASP.Net Core web service that is going to host the actor. It contains the implementation of the actor. An actor implementation is a class that derives from the base type `Actor` and implements the interfaces defined in the corresponding interfaces project. An actor class must also implement a constructor that accepts an `ActorService` instance and an `ActorId` and passes them to the base `Actor` class.

- The **actor client project (`\ActorClient`)** contains the implementation of the actor client which calls `DemoActor`'s methods defined in `IDemoActor`'s Interfaces.

## Running the example

To run the actor service locally run this command in `DemoActor` directory:

```sh
 dapr run --dapr-http-port 3500 --app-id demo_actor --app-port 5010 dotnet run
```

The `DemoActor` service will listen on port `5010` for HTTP.

### Make client calls

The `ActorClient` project shows how to make client calls for actor using Remoting which provides a strongly typed invocation experience.
Run the client project from `ActorClient` directory as:

```sh
 dotnet run
 ```

 *Note: If you started the actor service with dapr port other than 3500, then set the environment variable DAPR_HTTP_PORT to the value of --dapr-http-port specified while starting the actor service before running the client in terminal.*
 ```
 On Windows: set DAPR_HTTP_PORT=<port>
 On Linux, MacOS: export DAPR_HTTP_PORT=<port>
 ```

### Invoke Actor method without Remoting over Http

You can invoke Actor methods without remoting (directly over http), if the Actor method accepts at-most one argument.
Actor runtime will deserialize the incoming request body from client and use it as method argument to invoke the actor method.
When making non-remoting calls Actor method arguments and return types are serialized, deserialized as JSON.

**Save Data**
Following curl call will save data for actor id "abc"
(below calls on MacOs, Linux & Windows are exactly the same except for escaping quotes on Windows for curl)

On Linux, MacOS:

```sh
curl -X POST http://127.0.0.1:3500/v1.0/actors/DemoActor/abc/method/SaveData -d '{ "PropertyA": "ValueA", "PropertyB": "ValueB" }'
```

 On Windows:

```sh
curl -X POST http://127.0.0.1:3500/v1.0/actors/DemoActor/abc/method/SaveData -d "{ \"PropertyA\": \"ValueA\", \"PropertyB\": \"ValueB\" }"

```

**Get Data**
Following curl call will get data for actor id "abc"
(below calls on MacOs, Linux & Windows are exactly the same except for escaping quotes on Windows for curl)

On Linux, MacOS:

```sh
curl -X POST http://127.0.0.1:3500/v1.0/actors/DemoActor/abc/method/GetData
```

On Windows:

```sh
curl -X POST http://127.0.0.1:3500/v1.0/actors/DemoActor/abc/method/GetData
```

### Build and push Docker image
You can build the docker image of `DemoActor` service by running the following commands in the `DemoActor` project directory:

``` Bash
dotnet publish --os linux --arch x64 /t:PublishContainer -p ContainerImageTags='"latest"' --self-contained
```

The build produce and image with tag `demo-actor:latest` and load it in the local registry. 
Now the image can be pushed to your remote Docker registry by running the following commands:

``` Bash
# Replace <your-docker-registry> with your Docker registry
docker tag demo-actor:latest <your-docker-registry>/demo-actor:latest

# Push the image to your Docker registry
docker push <your-docker-registry>/demo-actor:latest
```

### Deploy the Actor service to Kubernetes
#### Prerequisites
- A Kubernetes cluster with `kubectl` configured to access it.
- Dapr v1.14+ installed on the Kubernetes cluster. Follow the instructions [here](https://docs.dapr.io/getting-started/install-dapr-kubernetes/).
- A Docker registry where you pushed the `DemoActor` image.

#### Deploy the Actor service
For quick deployment you can install dapr in dev mode using the following command:

``` Bash
dapr init -k --dev
```

To deploy the `DemoActor` service to Kubernetes, you can use the provided Kubernetes manifest file `demo-actor.yaml` in the `DemoActor` project directory.
Before applying the manifest file, replace the image name in the manifest file with the image name you pushed to your Docker registry.

Part to update in `demo-actor.yaml`:
``` YAML
image: <your-docker-registry>/demoactor:latest
```

To install the application in `default` namespace, run the following command:

``` Bash
kubectl apply -f demo-actor.yaml
```

This will deploy the `DemoActor` service to Kubernetes. You can check the status of the deployment by running:

``` Bash
kubectl get pods -n default --watch
```

The manifest create 2 services:

- `demoactor` service: The service that hosts the `DemoActor` actor.
- `demoactor-dapr` service: The service that hosts the Dapr sidecar for the `DemoActor` actor.

### Make client calls to the deployed Actor service
To make client calls to the deployed `DemoActor` service, you can use the `ActorClient` project.
Before running the client, update the `DAPR_HTTP_PORT` environment variable in the `ActorClient` project directory to the port on which Dapr is running in the Kubernetes cluster.

On Linux, MacOS:
``` Bash
export DAPR_HTTP_PORT=3500
```

Than port-forward the `DemoActor` service to your local machine:

``` Bash
kubectl port-forward svc/demoactor 3500:3500
```

Now you can run the client project from the `ActorClient` directory:

``` Bash
dotnet run
```