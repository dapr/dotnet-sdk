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

    internal class ConverterUtils
    {
        public static TimeSpan ConvertTimeSpanFromDaprFormat(string valueString)
        {
            if (string.IsNullOrEmpty(valueString))
            {
                var never = TimeSpan.FromMilliseconds(-1);
                return never;
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
            var hoursSpan = spanOfValue.Slice(0, hIndex);
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
            if (value.Value >= TimeSpan.Zero)
            {
                var hours = (value.Value.Days * 24) + value.Value.Hours;
                stringValue = FormattableString.Invariant($"{hours}h{value.Value.Minutes}m{value.Value.Seconds}s{value.Value.Milliseconds}ms");
            }

            return stringValue;
        }
    }
}
