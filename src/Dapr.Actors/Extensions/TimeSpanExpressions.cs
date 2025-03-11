// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System;
using System.Text.RegularExpressions;

namespace Dapr.Actors.Extensions;

internal static class TimeSpanExpressions
{
    private static readonly Regex hourRegex = new Regex(@"(\d+)h", RegexOptions.Compiled);
    private static readonly Regex minuteRegex = new Regex(@"(\d+)m", RegexOptions.Compiled);
    private static readonly Regex secondRegex = new Regex(@"(\d+)s", RegexOptions.Compiled);
    private static readonly Regex millisecondRegex = new Regex(@"(\d+)q", RegexOptions.Compiled);

    private const string YearlyPrefixPeriod = "@yearly";
    private const string MonthlyPrefixPeriod = "@monthy";
    private const string WeeklyPrefixPeriod = "@weekly";
    private const string DailyPrefixPeriod = "@daily";
    private const string MidnightPrefixPeriod = "@midnight";
    private const string HourlyPrefixPeriod = "@hourly";
    private const string EveryPrefixPeriod = "@every";
    
    /// <summary>
    /// Validates whether a given string represents a parseable Golang duration string.
    /// </summary>
    /// <param name="interval">The duration string to parse.</param>
    /// <returns>True if the string represents a parseable interval duration; false if not.</returns>
    public static bool IsDurationString(this string interval)
    {
        interval = interval.Replace("ms", "q").Replace("@every ", string.Empty);
        return hourRegex.Match(interval).Success ||
               minuteRegex.Match(interval).Success ||
               secondRegex.Match(interval).Success ||
               millisecondRegex.Match(interval).Success;
    }

    /// <summary>
    /// Creates a TimeSpan value from the 
    /// </summary>
    /// <param name="period"></param>
    /// <returns></returns>
    public static TimeSpan FromPrefixedPeriod(this string period)
    {
        if (period.StartsWith(YearlyPrefixPeriod))
        {
            var dateTime = DateTime.UtcNow;
            return dateTime.AddYears(1) - dateTime;
        }

        if (period.StartsWith(MonthlyPrefixPeriod))
        {
            var dateTime = DateTime.UtcNow;
            return dateTime.AddMonths(1) - dateTime;
        }

        if (period.StartsWith(WeeklyPrefixPeriod))
        {
            return TimeSpan.FromDays(7);
        }

        if (period.StartsWith(DailyPrefixPeriod) || period.StartsWith(MidnightPrefixPeriod))
        {
            return TimeSpan.FromDays(1);
        }

        if (period.StartsWith(HourlyPrefixPeriod))
        {
            return TimeSpan.FromHours(1);
        }

        if (period.StartsWith(EveryPrefixPeriod))
        {
            
        }
    }

    public static TimeSpan FromPrefixedEveryPeriod(this string period)
    {
        
    }
}
