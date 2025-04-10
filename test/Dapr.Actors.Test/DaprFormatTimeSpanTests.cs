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

#pragma warning disable 0618
namespace Dapr.Actors.Test;

using System;
using System.Collections.Generic;
using System.IO;
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
            null,
        },
        new object[]
        {
            "P1Y1M1W1DT1H1M1S",
            new TimeSpan(403, 1, 1, 1, 0),
            null,
        },
        new object[]
        {
            "R3/P2W3DT4H59M",
            new TimeSpan(17, 4, 59, 0, 0),
            3,
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
            new TimeSpan(17, 4, 59, 0, 0),
            3,
            "R3/P17DT4H59M"
        },
        new object[]
        {
            new TimeSpan(0, 7, 23, 12, 0),
            null,
            "7h23m12s0ms"
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
    public void DaprReminderFormat_TimeSpan_Parsing(string valueString, TimeSpan expectedDuration, int? expectedRepetition)
    {
        (TimeSpan duration, int? repetition) = ConverterUtils.ConvertTimeSpanValueFromISO8601Format(valueString);
        Assert.Equal(expectedDuration, duration);
        Assert.Equal(expectedRepetition, repetition);
    }

    [Theory]
    [MemberData(nameof(DaprReminderTimeSpanToDaprISO8601Format))]
    public void DaprReminderFormat_ConvertFromTimeSpan_ToDaprFormat(TimeSpan period, int? repetitions, string expectedValue)
    {
        var actualValue = ConverterUtils.ConvertTimeSpanValueInISO8601Format(period, repetitions);
        Assert.Equal(expectedValue, actualValue);
    }
}
#pragma warning restore 0618
