# Dapr .NET SDK Streaming Pub/Sub Example

## Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr .NET SDK](https://docs.dapr.io/developing-applications/sdks/dotnet/)

## Running the example

To run the sample locally run this command in the DaprClient directory:

```sh
dapr run --dapr-http-port 3500 -- dotnet run
```

## Publishing Pub/Sub Events

```bash
curl -X POST http://localhost:3500/v1.0/publish/pubsub/topicA \
  -H "Content-Type: application/json" \
 -d '{
       "hello": "world"
     }'
```
