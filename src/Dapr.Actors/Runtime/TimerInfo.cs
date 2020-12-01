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
    using System.Text.Json.Serialization;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors.Resources;

    [JsonConverter(typeof(TimerInfoConverter))]
    internal class TimerInfo
    {
        private readonly TimeSpan minTimePeriod = Timeout.InfiniteTimeSpan;

        public TimerInfo(
            string callback,
            byte[] state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            this.ValidateDueTime("DueTime", dueTime);
            this.ValidatePeriod("Period", period);
            this.Callback = callback;
            this.Data = state;
            this.DueTime = dueTime;
            this.Period = period;
        }

        public string Callback { get; private set; }

        public TimeSpan DueTime { get; private set; }

        public TimeSpan Period { get; private set; }

        public byte[] Data { get; private set; }

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

    internal class TimerInfoConverter : JsonConverter<TimerInfo>
    {
        public override TimerInfo Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var dueTime = default(TimeSpan);
            var period = default(TimeSpan);
            var data = default(byte[]);
            string callback = "";

            using (JsonDocument document = JsonDocument.ParseValue(ref reader))
            {
                var json = document.RootElement.Clone();

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

                if (json.TryGetProperty("callback", out var callbackProperty))
                {
                    callback = callbackProperty.GetString();
                }

                return new TimerInfo(callback, data, dueTime, period);
            }
        }

        public override async void Write(
            Utf8JsonWriter writer,
            TimerInfo value,
            JsonSerializerOptions options)
        {
            using var stream = new MemoryStream();

            writer.WriteStartObject();
            if (value.DueTime != null)
            {
                writer.WriteString("dueTime", ConverterUtils.ConvertTimeSpanValueInDaprFormat(value.DueTime));
            }

            if (value.Period != null)
            {
                writer.WriteString("period", ConverterUtils.ConvertTimeSpanValueInDaprFormat(value.Period));
            }

            if (value.Data != null)
            {
                writer.WriteString("data", Convert.ToBase64String(value.Data));
            }

            if (value.Callback != null)
            {
                writer.WriteString("callback", value.Callback);
            }

            writer.WriteEndObject();
            await writer.FlushAsync();
        }
    }        
}
