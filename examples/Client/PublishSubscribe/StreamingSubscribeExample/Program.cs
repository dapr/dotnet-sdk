// See https://aka.ms/new-console-template for more information

using System.Text.Json;
using System.Text.Json.Serialization;
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

using var subscriptionA = client.Subscribe(
    "pubsub",
    "topicA",
    (request, cancellationToken) =>
    {
        Console.WriteLine($"Received message on topic A: {request}");

        Console.WriteLine($"Data is: {JsonSerializer.Deserialize<TopicData>(request.Data.Span)}");
        
        return Task.FromResult(TopicResponse.Drop);
    });

Console.WriteLine("Subscribing to topic B...");

using var subscriptionB = client.Subscribe(
    "pubsub",
    "topicB",
    (request, cancellationToken) =>
    {
        Console.WriteLine($"Received message on topic B: {request}");

        Console.WriteLine($"Data is: {JsonSerializer.Deserialize<TopicData>(request.Data.Span)}");

        return Task.FromResult(TopicResponse.Success);
    });

Console.WriteLine("Waiting 30s to exit...");

await Task.Delay(TimeSpan.FromSeconds(30));

Console.WriteLine("Exiting...");

sealed record TopicData
{
    [JsonPropertyName("test")]
    public string? Test { get; init; }
}
