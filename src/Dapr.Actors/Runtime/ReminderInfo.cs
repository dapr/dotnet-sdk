// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Runtime
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors.Resources;

    internal class ReminderInfo
    {
        private readonly TimeSpan minTimePeriod = Timeout.InfiniteTimeSpan;

        public ReminderInfo(
            byte[] state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            this.ValidateDueTime("DueTime", dueTime);
            this.ValidatePeriod("Period", period);
            this.Data = state;
            this.DueTime = dueTime;
            this.Period = period;
        }

        public TimeSpan DueTime { get; private set; }

        public TimeSpan Period { get; private set; }

        public byte[] Data { get; private set; }

        internal static async Task<ReminderInfo> DeserializeAsync(Stream stream)
        {
            var json = await JsonSerializer.DeserializeAsync<JsonElement>(stream);

            var dueTime = default(TimeSpan);
            var period = default(TimeSpan);
            var data = default(byte[]);

            if (json.TryGetProperty("dueTime", out var dueTimeProperty))
            {
                var dueTimeString = dueTimeProperty.GetString();
                dueTime = ConverterUtils.ConvertTimeSpanFromDaprFormat(dueTimeString);
            }

            if (json.TryGetProperty("period", out var periodProperty))
            {
                var periodString = periodProperty.GetString();
                period = ConverterUtils.ConvertTimeSpanFromDaprFormat(periodString);
            }

            if (json.TryGetProperty("data", out var dataProperty) && dataProperty.ValueKind != JsonValueKind.Null)
            {
                data = dataProperty.GetBytesFromBase64();
            }

            return new ReminderInfo(data, dueTime, period);
        }

        internal async Task<string> SerializeAsync()
        {
            using var stream = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();
            if (this.DueTime != null)
            {
                writer.WriteString("dueTime", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.DueTime));
            }

            if (this.Period != null)
            {
                writer.WriteString("period", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.Period));
            }

            if (this.Data != null)
            {
                writer.WriteString("data", Convert.ToBase64String(this.Data));
            }

            writer.WriteEndObject();
            await writer.FlushAsync();
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private void ValidateDueTime(string argName, TimeSpan value)
        {
            if (value < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    argName,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        SR.TimerArgumentOutOfRange,
                        this.minTimePeriod.TotalMilliseconds,
                        TimeSpan.MaxValue.TotalMilliseconds));
            }
        }

        private void ValidatePeriod(string argName, TimeSpan value)
        {
            if (value < this.minTimePeriod)
            {
                throw new ArgumentOutOfRangeException(
                    argName,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        SR.TimerArgumentOutOfRange,
                        this.minTimePeriod.TotalMilliseconds,
                        TimeSpan.MaxValue.TotalMilliseconds));
            }
        }
    }
}
