# Dapr Actor example

The Actor example shows how to create a virtual actor (`DemoActor`) and invoke its methods on the client application.

## Prerequisites

- [.NET Core 3.1 or .NET 5+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/)

## Projects in sample
- The **model project (`\PubSub.Domain`)** contains the model definition for the pub&sub.
- The **publish project(`\Publisher`)** contains the way how to publish a message to the specified topic.
- The **subscribe project (`\Subscriber`)** contains the way how to subscribe a topic and receive message.
## Running the example

To run the pubsub service locally 

run this command in `Publisher` directory:
```sh
dapr run --app-id Publisher --app-port 5000 --components-path ./components dotnet run
```
run this command in `Subscriber` directory:
```sh
dapr run --app-id Subscriber --app-port 5001 --components-path ./components dotnet run
```

The `Publisher` service will listen on port `5000` for HTTP.
The `Subscriber` service will listen on port `5001` for HTTP.

*Note: For Running the sample with ISS express, change the launchsettings.json to use 127.0.0.1 instead of localhost.*

### Make client calls

The `Publisher` project shows 
how to make client publish a event for pub&sub using Redis default.

The `Sublisher` project shows
how to subscribe a topic use yaml,and deal with event.

`See ./components/subscription.yaml`.

reference to the doc [how to publish&subscribe](https://docs.dapr.io/developing-applications/building-blocks/pubsub/howto-publish-subscribe/)


**Publish**
Following curl call will get data for actor id "abc"
(below calls on MacOs, Linux & Windows are exactly the same except for escaping quotes on Windows for curl)

On Linux, MacOS:

```sh
curl -X POST "http://localhost:5000/Order" -d ""
```

On Windows:

```sh
curl -X POST "http://localhost:5000/Order" -d ""
```

**Subscribe**
You can see the information in console.
```
== APP == ====================
== APP == OrderId:              f6ab0046-9bcc-4243-8f77-9f05c02873b0
== APP == OldValue:             this is old value
== APP == NewValue:             this is new value
== APP == OrderUpdateTime:      08/12/2021 13:19:04
== APP == ====================
```