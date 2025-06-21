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

using Dapr.Actors.Extensions;

namespace Dapr.Actors.Runtime;

using System;
using System.Text;
using System.Text.RegularExpressions;

internal static class ConverterUtils
{
    private static Regex regex = new("^(R(?<repetition>\\d+)/)?P((?<year>\\d+)Y)?((?<month>\\d+)M)?((?<week>\\d+)W)?((?<day>\\d+)D)?(T((?<hour>\\d+)H)?((?<minute>\\d+)M)?((?<second>\\d+)S)?)?$", RegexOptions.Compiled);
    public static TimeSpan ConvertTimeSpanFromDaprFormat(string valueString)
    {
        if (string.IsNullOrEmpty(valueString))
        {
            var never = TimeSpan.FromMilliseconds(-1);
            return never;
        }
        
        if (valueString.IsDurationExpression())
        {
            return valueString.FromPrefixedPeriod();
        }

        // TimeSpan is a string. Format returned by Dapr is: 1h4m5s4ms4us4ns
        //  acceptable values are: m, s, ms, us(micro), ns
        var spanOfValue = valueString.AsSpan();

        // Change the value returned by Dapr runtime, so that it can be parsed with TimeSpan.
        // Format returned by Dapr runtime: 4h15m50s60ms. It doesnt have days.
        // Dapr runtime should handle timespans in ISO 8601 format.
        // Replace ms before m & s. Also append 0 days for parsing correctly with TimeSpan
        int hIndex = spanOfValue.IndexOf('h');
        int mIndex = spanOfValue.IndexOf('m');
        int sIndex = spanOfValue.IndexOf('s');
        int msIndex = spanOfValue.IndexOf("ms");

        // handle days from hours.
        var hoursSpan = spanOfValue[..hIndex];
        var hours = int.Parse(hoursSpan);
        var days = hours / 24;
        hours %= 24;

        var minutesSpan = spanOfValue[(hIndex + 1)..mIndex];
        var minutes = int.Parse(minutesSpan);

        var secondsSpan = spanOfValue[(mIndex + 1)..sIndex];
        var seconds = int.Parse(secondsSpan);

        var millisecondsSpan = spanOfValue[(sIndex + 1)..msIndex];
        var milliseconds = int.Parse(millisecondsSpan);

        return new TimeSpan(days, hours, minutes, seconds, milliseconds);
    }

    public static string ConvertTimeSpanValueInDaprFormat(TimeSpan? value)
    {
        // write in format expected by Dapr, it only accepts h, m, s, ms, us(micro), ns
        var stringValue = string.Empty;
        if (value is null)
            return stringValue;
            
        if (value.Value >= TimeSpan.Zero)
        {
            var hours = (value.Value.Days * 24) + value.Value.Hours;
            stringValue = FormattableString.Invariant($"{hours}h{value.Value.Minutes}m{value.Value.Seconds}s{value.Value.Milliseconds}ms");
        }

        return stringValue;
    }
        
    public static string ConvertTimeSpanValueInISO8601Format(TimeSpan value, int? repetitions)
    {
        StringBuilder builder = new StringBuilder();

        if (repetitions == null)
        {
            return ConvertTimeSpanValueInDaprFormat(value);
        }

        if (value.Milliseconds > 0)
        {
            throw new ArgumentException("The TimeSpan value, combined with repetition cannot be in milliseconds.", nameof(value));
        }

        builder.Append($"R{repetitions}/P");

        if(value.Days > 0)
        {
            builder.Append($"{value.Days}D");
        }

        builder.Append('T');

        if(value.Hours > 0)
        {
            builder.Append($"{value.Hours}H");
        }

        if(value.Minutes > 0)
        {
            builder.Append($"{value.Minutes}M");
        }

        if(value.Seconds > 0)
        {
            builder.Append($"{value.Seconds}S");
        }
        return builder.ToString();
    }
        
    public static (TimeSpan, int?) ConvertTimeSpanValueFromISO8601Format(string valueString)
    {
        // ISO 8601 format can be Rn/PaYbMcHTdHeMfS or PaYbMcHTdHeMfS so if it does 
        // not start with R or P then assuming it to default Dapr format without repetition
        if (!(valueString.StartsWith('R') || valueString.StartsWith('P')))
        {
            return (ConvertTimeSpanFromDaprFormat(valueString), -1);
        }

        var matches = regex.Match(valueString);
            
        var repetition = matches.Groups["repetition"].Success ? int.Parse(matches.Groups["repetition"].Value) : (int?)null;
        

        var days = 0;
        var year = matches.Groups["year"].Success ? int.Parse(matches.Groups["year"].Value) : 0;
        days = year * 365;

        var month = matches.Groups["month"].Success ? int.Parse(matches.Groups["month"].Value) : 0;            
        days += month * 30;
            
        var week = matches.Groups["week"].Success ? int.Parse(matches.Groups["week"].Value) : 0;
        days += week * 7;

        var day = matches.Groups["day"].Success ? int.Parse(matches.Groups["day"].Value) : 0;
        days += day;

        var hour = matches.Groups["hour"].Success ? int.Parse(matches.Groups["hour"].Value) : 0;
        var minute = matches.Groups["minute"].Success ? int.Parse(matches.Groups["minute"].Value) : 0;
        var second = matches.Groups["second"].Success ? int.Parse(matches.Groups["second"].Value) : 0;

        return (new TimeSpan(days, hour, minute, second), repetition);
    }
}
