// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

#pragma warning disable 0618
namespace Dapr.Actors.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Dapr.Actors.Runtime;
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

        public static readonly IEnumerable<object[]> DaprReminderISO8601FormatTimeSpanAndExpectedDeserializedValues = new List<object[]>
        {
            new object[]
            {
                "R10/PT10S",
                new TimeSpan(0, 0, 0, 10, 0),
                10
            },
            new object[]
            {
                "PT10H5M10S",
                new TimeSpan(0, 10, 5, 10, 0),
                -1,
            },
            new object[]
            {
                "4h15m50s60ms",
                new TimeSpan(0, 4, 15, 50, 60),
                -1,
            }
        };

        public static readonly IEnumerable<object[]> DaprReminderTimeSpanToDaprISO8601Format = new List<object[]>
        {
            new object[]
            {
                new TimeSpan(10, 10, 10, 10),
                1,
                "R1/P10DT10H10M10S"
            },
            new object[]
            {
                new TimeSpan(10, 10, 10, 10),
                -1,
                "250h10m10s0ms"
            }
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
            var deserializedTimeSpan = ConverterUtils.ConvertTimeSpanFromDaprFormat(timespanString);

            Assert.Equal(expectedDeserializedValue, deserializedTimeSpan);
        }

        [Fact]
        public async Task ConverterUtilsTestEndToEndAsync()
        {
            static Task<string> SerializeAsync(TimeSpan dueTime, TimeSpan period)
            {
                var timerInfo = new TimerInfo(
                    callback: null,
                    state: null,
                    dueTime: dueTime,
                    period: period);
                return Task.FromResult(System.Text.Json.JsonSerializer.Serialize<TimerInfo>(timerInfo));
            }

            var inTheFuture = TimeSpan.FromMilliseconds(20);
            var never = TimeSpan.FromMilliseconds(-1);
            Assert.Equal(
               "{\"dueTime\":\"0h0m0s20ms\"}",
               await SerializeAsync(inTheFuture, never));
        }

        [Fact]
        public void DaprFormatTimespanEmpty()
        {
            Func<string, TimeSpan> convert = ConverterUtils.ConvertTimeSpanFromDaprFormat;
            TimeSpan never = TimeSpan.FromMilliseconds(-1);
            Assert.Equal<TimeSpan>(never, convert(null));
            Assert.Equal<TimeSpan>(never, convert(string.Empty));
        }

        [Theory]
        [MemberData(nameof(DaprReminderISO8601FormatTimeSpanAndExpectedDeserializedValues))]
        public void DaprReminderFormat_TimeSpan_Parsing(string valueString, TimeSpan expectedDuration, int expectedRepetition)
        {
            (TimeSpan duration, int repetition) = ConverterUtils.ConvertTimeSpanValueFromISO8601Format(valueString);
            Assert.Equal(expectedDuration, duration);
            Assert.Equal(expectedRepetition, repetition);
        }

        [Theory]
        [MemberData(nameof(DaprReminderTimeSpanToDaprISO8601Format))]
        public void DaprReminderFormat_ConvertFromTimeSpan_ToDaprFormat(TimeSpan period, int repetitions, string expectedValue)
        {
            var actualValue = ConverterUtils.ConvertTimeSpanValueInISO8601Format(period, repetitions);
            Assert.Equal(expectedValue, actualValue);
        }
    }
}
#pragma warning restore 0618
