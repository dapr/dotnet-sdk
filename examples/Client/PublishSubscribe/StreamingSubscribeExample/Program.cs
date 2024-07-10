// See https://aka.ms/new-console-template for more information

using Dapr.Messaging.PublishSubscribe;

var client = DaprPublishSubscribeClient.Create();

Console.WriteLine("Subscribing to topic A...");

var subscriptionA = client.SubscribeAsync(
    "pubsub",
    "topicA",
    (request, cancellationToken) =>
    {
        Console.WriteLine($"Received message on topic A: {request}");

        return Task.FromResult(TopicResponse.Success);
    });

Console.WriteLine("Subscribing to topic B...");

var subscriptionB = client.SubscribeAsync(
    "pubsub",
    "topicB",
    (request, cancellationToken) =>
    {
        Console.WriteLine($"Received message on topic B: {request}");

        return Task.FromResult(TopicResponse.Success);
    });

Console.WriteLine("Waiting for messages or completion...");

await Task.WhenAll(subscriptionA, subscriptionB);

Console.WriteLine("Done.");
