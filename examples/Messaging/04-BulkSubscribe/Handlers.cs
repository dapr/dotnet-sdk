using Dapr.Messaging;
using Dapr.Messaging.PublishSubscribe;

namespace BulkSubscribe.Example04;

/// <summary>
/// High-throughput telemetry subscriber with BulkSubscribe enabled.
/// Dapr batches up to 50 messages or waits up to 500ms before delivering them.
/// </summary>
[DaprTopic("pubsub", "telemetry", BulkSubscribe = true, MaxMessagesCount = 50, MaxAwaitDurationMs = 500)]
public class TelemetryBulkHandler : ITopicHandler<DeviceTelemetry>
{
    private readonly ILogger<TelemetryBulkHandler> logger;

    public TelemetryBulkHandler(ILogger<TelemetryBulkHandler> logger)
    {
        this.logger = logger;
    }

    public Task<TopicResponseAction> HandleAsync(DeviceTelemetry reading, TopicContext context, CancellationToken cancellationToken)
    {
        this.logger.LogInformation(
            "Processed telemetry reading from {DeviceId}: Temp={Temp:F1}°C, Humidity={Hum:F1}% (MsgId: {MsgId})",
            reading.DeviceId, reading.TemperatureCelsius, reading.HumidityPercent, context.MessageId);

        // Discard invalid sensor readings (e.g. sensor malfunction)
        if (reading.TemperatureCelsius < -273.15 || reading.HumidityPercent < 0 || reading.HumidityPercent > 100)
        {
            this.logger.LogWarning("Dropping out-of-range sensor reading for device {DeviceId}", reading.DeviceId);
            return Task.FromResult(TopicResponseAction.Drop);
        }

        return Task.FromResult(TopicResponseAction.Success);
    }
}
