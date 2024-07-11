// See https://aka.ms/new-console-template for more information

using Dapr.Messaging.PublishSubscribe;
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(
    builder =>
    {
        builder.SetMinimumLevel(LogLevel.Debug);
        builder.AddConsole();
    });

var client = DaprPublishSubscribeClient.Create(new() { LoggerFactory = loggerFactory });

Console.WriteLine("Subscribing to topic A...");

var subscriptionA = client.SubscribeAsync(
    "pubsub",
    "topicA",
    (request, cancellationToken) =>
    {
        Console.WriteLine($"Received message on topic A: {request}");

        return Task.FromResult(TopicResponse.Drop);
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
