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

namespace Dapr.Actors.Runtime
{
    using System;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;

    // represents the wire format used by Dapr to store reminder info with the runtime
    internal struct ReminderInfo
    {
        public ReminderInfo(
            byte[] data,
            TimeSpan dueTime,
            TimeSpan period)
        {
            this.Data = data;
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

        internal async ValueTask<string> SerializeAsync()
        {
            using var stream = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream);

            writer.WriteStartObject();
            writer.WriteString("dueTime", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.DueTime));
            writer.WriteString("period", ConverterUtils.ConvertTimeSpanValueInDaprFormat(this.Period));
            writer.WriteBase64String("data", this.Data);
            writer.WriteEndObject();
            await writer.FlushAsync();
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
