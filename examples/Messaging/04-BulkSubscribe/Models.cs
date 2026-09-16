namespace BulkSubscribe.Example04;

/// <summary>
/// High-frequency IoT device sensor telemetry reading.
/// </summary>
public record DeviceTelemetry(
    string DeviceId,
    double TemperatureCelsius,
    double HumidityPercent,
    long TimestampUnixMs);
