// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Runtime;

using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using Dapr.Actors.Resources;

/// <summary>
/// Represents the details of the timer set on an Actor.
/// </summary>
[Obsolete("This class is an implementation detail of the framework and will be made internal in a future release.")]
[JsonConverter(typeof(TimerInfoConverter))]
public class TimerInfo
{
    private readonly TimeSpan minTimePeriod = Timeout.InfiniteTimeSpan;

    internal TimerInfo(
        string callback,
        byte[] state,
        TimeSpan dueTime,
        TimeSpan period,
        TimeSpan? ttl = null)
    {
        this.ValidateDueTime("DueTime", dueTime);
        this.ValidatePeriod("Period", period);
        this.Callback = callback;
        this.Data =  state;
        this.DueTime = dueTime;
        this.Period = period;
        this.Ttl = ttl;
    }

    internal string Callback { get; private set; }

    internal TimeSpan DueTime { get; private set; }

    internal TimeSpan Period { get; private set; }

    internal byte[] Data { get; private set; }

    internal TimeSpan? Ttl { get; private set; }

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
#pragma warning disable 0618
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
        string callback = null;
        TimeSpan? ttl = null;

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

            if (json.TryGetProperty("ttl", out var ttlProperty))
            {
                var ttlString = ttlProperty.GetString();
                ttl = ConverterUtils.ConvertTimeSpanFromDaprFormat(ttlString);
            }

            return new TimerInfo(callback, data, dueTime, period, ttl);
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

        if (value.Period != null && value.Period >= TimeSpan.Zero)
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

        if (value.Ttl != null)
        {
            writer.WriteString("ttl", ConverterUtils.ConvertTimeSpanValueInDaprFormat(value.Ttl));
        }

        writer.WriteEndObject();
        await writer.FlushAsync();
    }
}
#pragma warning restore 0618