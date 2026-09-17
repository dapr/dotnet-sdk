using System.Text;
using System.Text.Json;
using Dapr;
using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

namespace DynamicStreaming.Example07;

/// <summary>
/// Background worker demonstrating dynamic runtime streaming subscriptions using
/// <see cref="DaprPublishSubscribeClient.SubscribeAsync"/>.
/// </summary>
public class DynamicSubscriberWorker : BackgroundService
{
    private readonly DaprPublishSubscribeClient client;
    private readonly ILogger<DynamicSubscriberWorker> logger;

    public DynamicSubscriberWorker(DaprPublishSubscribeClient client, ILogger<DynamicSubscriberWorker> logger)
    {
        this.client = client;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        const string pubsubName = "pubsub";
        const string dynamicTopic = "tenant-events";

        this.logger.LogInformation("Starting dynamic subscription loop for topic {Topic}...", dynamicTopic);

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = new DaprSubscriptionOptions(
                new MessageHandlingPolicy(
                    TimeoutDuration: TimeSpan.FromSeconds(10),
                    DefaultResponseAction: TopicResponseAction.Retry))
            {
                DeadLetterTopic = "tenant-events-dlq",
                ErrorHandler = ex =>
                {
                    this.logger.LogWarning("Subscription background error: {Message}", ex.InnerException?.Message ?? ex.Message);
                    return Task.CompletedTask;
                }
            };

            try
            {
                await using var subscription = await this.client.SubscribeAsync(
                    pubsubName,
                    dynamicTopic,
                    options,
                    this.HandleDynamicMessageAsync,
                    stoppingToken);

                this.logger.LogInformation("Dynamic subscription opened to {Topic}. Awaiting stream completion...", dynamicTopic);

                var completion = ((IDaprSubscription)subscription).Completion;
                await completion.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                this.logger.LogInformation("Stopping dynamic subscription loop (cancellation requested).");
                break;
            }
            catch (AggregateException ex)
            {
                this.logger.LogError(ex, "Dynamic subscription faulted (handler error): {Message}", ex.Flatten().InnerException?.Message);
            }
            catch (DaprException ex)
            {
                this.logger.LogError(ex, "Dynamic subscription stream faulted: {Message}", ex.InnerException?.Message ?? ex.Message);
            }
            catch (Exception ex)
            {
                this.logger.LogError(ex, "Unexpected error in dynamic subscription supervisor: {Message}", ex.Message);
            }

            // Exponential / backoff delay before reconnecting
            if (!stoppingToken.IsCancellationRequested)
            {
                this.logger.LogInformation("Reconnecting dynamic subscription in 3 seconds...");
                try { await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    public Task<TopicResponseAction> HandleDynamicMessageAsync(TopicMessage message, CancellationToken cancellationToken)
    {
        try
        {
            var json = Encoding.UTF8.GetString(message.Data.Span);
            this.logger.LogInformation(
                "[DYNAMIC STREAM] Received message on topic {Topic} (MsgId: {Id}): {Payload}",
                message.Topic, message.Id, json);

            var @event = JsonSerializer.Deserialize<TenantEvent>(message.Data.Span);
            if (@event is null || string.IsNullOrWhiteSpace(@event.TenantId))
            {
                this.logger.LogWarning("Dropping invalid tenant event: missing TenantId");
                return Task.FromResult(TopicResponseAction.Drop);
            }

            if (@event.EventType.StartsWith("RETRY-", StringComparison.OrdinalIgnoreCase))
            {
                this.logger.LogWarning("Simulating transient failure for {EventType} (Retry)", @event.EventType);
                return Task.FromResult(TopicResponseAction.Retry);
            }

            return Task.FromResult(TopicResponseAction.Success);
        }
        catch (JsonException ex)
        {
            this.logger.LogError(ex, "Malformed JSON payload — dropping message to DLQ");
            return Task.FromResult(TopicResponseAction.Drop);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Transient processing error — requesting redelivery");
            return Task.FromResult(TopicResponseAction.Retry);
        }
    }
}
