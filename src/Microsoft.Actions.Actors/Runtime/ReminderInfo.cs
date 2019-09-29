// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading;
    using Microsoft.Actions.Actors;
    using Microsoft.Actions.Actors.Resources;
    using Newtonsoft.Json;

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

        internal static ReminderInfo Deserialize(Stream stream)
        {
            // Deserialize using JsonReader as we know the property names in response. Deserializing using JsonReader is most performant.
            using var streamReader = new StreamReader(stream);
            using var reader = new JsonTextReader(streamReader);
            return reader.Deserialize(GetFromJsonProperties);
        }

        internal string SerializeToJson()
        {
            string content;
            using (var sw = new StringWriter())
            {
                using var writer = new JsonTextWriter(sw);
                writer.WriteStartObject();
                if (this.DueTime != null)
                {
                    writer.WriteProperty((TimeSpan?)this.DueTime, "dueTime", JsonWriterExtensions.WriteTimeSpanValueActionsFormat);
                }

                if (this.Period != null)
                {
                    writer.WriteProperty((TimeSpan?)this.Period, "period", JsonWriterExtensions.WriteTimeSpanValueActionsFormat);
                }

                if (this.Data != null)
                {
                    writer.WriteProperty(Convert.ToBase64String(this.Data), "data", JsonWriterExtensions.WriteStringValue);
                }

                writer.WriteEndObject();

                content = sw.ToString();
            }

            return content;
        }

        private static ReminderInfo GetFromJsonProperties(JsonReader reader)
        {
            var dueTime = default(TimeSpan);
            var period = default(TimeSpan);
            var data = default(byte[]);

            do
            {
                var propName = reader.ReadPropertyName();
                if (string.Compare("dueTime", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    dueTime = (TimeSpan)reader.ReadValueAsTimeSpanActionsFormat();
                }
                else if (string.Compare("period", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    period = (TimeSpan)reader.ReadValueAsTimeSpanActionsFormat();
                }
                else if (string.Compare("data", propName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    var stringData = reader.ReadValueAsString();
                    data = stringData == null ? null : Encoding.UTF8.GetBytes(stringData);
                }
                else
                {
                    reader.SkipPropertyValue();
                }
            }
            while (reader.TokenType != JsonToken.EndObject);

            return new ReminderInfo(data, dueTime, period);
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
