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

using var subscriptionA = client.SubscribeAsync(
    "pubsub",
    "topicA",
    (request, cancellationToken) =>
    {
        Console.WriteLine($"Received message on topic A: {request}");

        return Task.FromResult(TopicResponse.Drop);
    });

Console.WriteLine("Subscribing to topic B...");

using var subscriptionB = client.SubscribeAsync(
    "pubsub",
    "topicB",
    (request, cancellationToken) =>
    {
        Console.WriteLine($"Received message on topic B: {request}");

        return Task.FromResult(TopicResponse.Success);
    });

Console.WriteLine("Waiting 30s to exit...");

await Task.Delay(TimeSpan.FromSeconds(30));

Console.WriteLine("Exiting...");
