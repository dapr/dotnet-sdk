// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Dapr.Actors.Common;
    using Newtonsoft.Json;
    using Xunit;

    public class DaprFormatTimeSpanTests
    {
        public static readonly IEnumerable<object[]> DaprFormatTimeSpanJsonStringsAndExpectedDeserializedValues = new List<object[]>
        {
            new object[]
            {
                "{\"dueTime\":\"4h15m50s60ms\"",
                new TimeSpan(0, 4, 15, 50, 60),
            },
            new object[]
            {
                "{\"dueTime\":\"0h35m10s12ms\"",
                new TimeSpan(0, 0, 35, 10, 12),
            },
        };

        [Theory]
        [MemberData(nameof(DaprFormatTimeSpanJsonStringsAndExpectedDeserializedValues))]
        public void DaprFormat_TimeSpan_Parsing(string daprFormatTimeSpanJsonString, TimeSpan expectedDeserializedValue)
        {
            using var textReader = new StringReader(daprFormatTimeSpanJsonString);
            using var jsonTextReader = new JsonTextReader(textReader);

            while (jsonTextReader.TokenType != JsonToken.String)
            {
                jsonTextReader.Read();
            }

            var timespanString = (string)jsonTextReader.Value;
            var deserializedTimeSpan = ConvertTimeSpanFromDaprFormat(timespanString);

            Assert.Equal(expectedDeserializedValue, deserializedTimeSpan);
        }
    }
}