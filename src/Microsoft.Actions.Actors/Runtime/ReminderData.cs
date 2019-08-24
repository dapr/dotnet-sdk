// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.Actions.Actors;
    using Newtonsoft.Json;

    internal class ReminderData
    {
        private ReminderData()
        {
        }

        public TimeSpan DueTime { get; private set; }

        public TimeSpan Period { get; private set; }

        public byte[] Data { get; private set; }

        internal static ReminderData Deserialize(Stream stream)
        {
            // Deserialize using JsonReader as we know the property names in response. Deserializing using JsonReader is most performant.
            using (var streamReader = new StreamReader(stream))
            {
                using (var reader = new JsonTextReader(streamReader))
                {
                    return reader.Deserialize(GetFromJsonProperties);
                }
            }
        }

        internal static void Serialize(JsonWriter writer, ReminderData obj)
        {
            // Required properties are always serialized, optional properties are serialized when not null.
            writer.WriteStartObject();
            if (obj.DueTime != null)
            {
                writer.WriteProperty((TimeSpan?)obj.DueTime, "dueTime", JsonWriterExtensions.WriteTimeSpanValueActionsFormat);
            }

            if (obj.Period != null)
            {
                writer.WriteProperty((TimeSpan?)obj.Period, "period", JsonWriterExtensions.WriteTimeSpanValueActionsFormat);
            }

            if (obj.Data != null)
            {
                writer.WriteProperty(Convert.ToBase64String(obj.Data), "data", JsonWriterExtensions.WriteStringValue);
            }

            writer.WriteEndObject();
        }

        private static ReminderData GetFromJsonProperties(JsonReader reader)
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
                    data = Encoding.UTF8.GetBytes(reader.ReadValueAsString());
                }
                else
                {
                    reader.SkipPropertyValue();
                }
            }
            while (reader.TokenType != JsonToken.EndObject);

            return new ReminderData()
            {
                DueTime = dueTime,
                Period = period,
                Data = data,
            };
        }
    }
}
