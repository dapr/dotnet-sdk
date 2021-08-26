# Dapr PubSub example

The PubSub example 
shows how to create a service which will publish the event(`Publisher`).

shows how to receive the event by Declarative using yaml file(`DeclaretiveSubscriber`).

shows how to receive the event by Programmatic using coding definition(`ProgrammaticSubscriber`).
## Prerequisites

- [.NET Core 3.1 or .NET 5+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)

## Projects in sample
- The **model project (`\PubSub.Domain`)** contains the model definition for the pubsub.
- The **publish project(`\Publisher`)** shows how to publish a message to the specified topic.
- The **subscribe project (`\DeclarativeSubscriber`)** shows how to subscribe to a topic and receive messages using the Declarative method.
- The **subscribe project (`\ProgrammaticSubscriber`)** shows how to subscribe to a topic and receive messages using the Programmatic method.
- The **Subscribe project (`\ProgrammaticSubscriberDotNet`)** shows how to subscribe to a topic and receive messages using the SDK.
## Running the example

To run the pub&sub service locally 

run this command in `Publisher` directory:
```sh
dapr run --app-id Publisher --app-port 5000 --components-path ./components dotnet run
```
run this command in `DeclarativeSubscriber` directory:
```sh
dapr run --app-id DeclarativeSubscriber --app-port 5001 --components-path ./components dotnet run
```
run this command in `ProgrammaticSubscriber` directory:
```sh
dapr run --app-id ProgrammaticSubscriber --app-port 5002 --components-path ./components dotnet run
```
run this command in `ProgrammaticSubscriberDotNet` directory:
```sh
dapr run --app-id ProgrammaticSubscriberDotNet --app-port 5003 --components-path ./components dotnet run
```

The `Publisher` service will listen on port `5000` for HTTP.
The `DeclaretiveSubscriber` service will listen on port `5001` for HTTP.
The `ProgrammaticSubscriber` service will listen on port `5002` for HTTP.
The `ProgrammaticSubscriberDotNet` service will listen on port `5003` for HTTP.

*Note: For Running the sample with IIS express, change the launchsettings.json to use 127.0.0.1 instead of localhost.*

### Make client calls

The `Publisher` project shows how to make client publish an event for pub&sub using Redis default. The `DeclaretiveSubscriber` project shows how to subscribe a topic use yaml, and deal with event. The `ProgrammaticSubscriber` project shows how to subscribe a topic by using program, and deal with event. The `ProgrammaticSubscriberDotNet` project shows how to subscribe a topic by using dotnet SDK, and deal with event.


`See ./components/subscription.yaml`.

reference to the doc [how to publish&subscribe](https://docs.dapr.io/developing-applications/building-blocks/pubsub/howto-publish-subscribe/)


**Publish**

Following curl call will publish a custom message.

(below calls on MacOs, Linux & Windows are exactly the same except for escaping quotes on Windows for curl)

On Linux, MacOS:

```sh
curl -X POST "http://localhost:5000/Order?newOwnerId=EE3845EB-B734-44D6-AB5A-1956A05B9E95" -d ''
```

On Windows:

```sh
curl -X POST "http://localhost:5000/Order?newOwnerId=EE3845EB-B734-44D6-AB5A-1956A05B9E95" -d ''
```

**Subscribe**
You can see the information in console.
```
== APP == ====================
== APP == OrderId:           31880a7d-78bb-4e33-ac9a-b11756610435
== APP == Type:              OwnerId
== APP == OldValue:          ee3845eb-b734-44d6-ab5a-1956a05b9e96
== APP == NewValue:          ee3845eb-b734-44d6-ab5a-1956a05b9e95
== APP == OrderUpdateTime:   08/26/2021 11:07:42
== APP == ====================
```