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
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Dapr.Actors.Extensions;

internal static class DurationExtensions
{
    /// <summary>
    /// Used to parse the duration string accompanying an @every expression.
    /// </summary>
    private static readonly Regex durationRegex = new(@"(?<value>\d+(\.\d+)?)(?<unit>ns|us|µs|ms|s|m|h)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    /// <summary>
    /// A regular expression used to evaluate whether a given prefix period embodies an @every statement.
    /// </summary>
    private static readonly Regex isEveryExpression = new(@"^@every (\d+(\.\d+)?(ns|us|µs|ms|s|m|h))+$");
    /// <summary>
    /// The various acceptable duration values for a period expression.
    /// </summary>
    private static readonly string[] acceptablePeriodValues =
    {
        "yearly", "monthly", "weekly", "daily", "midnight", "hourly"
    };

    private const string YearlyPrefixPeriod = "@yearly";
    private const string MonthlyPrefixPeriod = "@monthly";
    private const string WeeklyPrefixPeriod = "@weekly";
    private const string DailyPrefixPeriod = "@daily";
    private const string MidnightPrefixPeriod = "@midnight";
    private const string HourlyPrefixPeriod = "@hourly";
    private const string EveryPrefixPeriod = "@every";

    /// <summary>
    /// Indicates that the schedule represents a prefixed period expression.
    /// </summary>
    /// <param name="expression"></param>
    /// <returns></returns>
    public static bool IsDurationExpression(this string expression) => expression.StartsWith('@') &&
                                                                       (isEveryExpression.IsMatch(expression) ||
                                                                        expression.EndsWithAny(acceptablePeriodValues, StringComparison.InvariantCulture));

    /// <summary>
    /// Creates a TimeSpan value from the prefixed period value. 
    /// </summary>
    /// <param name="period">The prefixed period value to parse.</param>
    /// <returns>A TimeSpan value matching the provided period.</returns>
    public static TimeSpan FromPrefixedPeriod(this string period)
    {
        if (period.StartsWith(YearlyPrefixPeriod))
        {
            var dateTime = DateTime.UtcNow;
            return dateTime.AddYears(1) - dateTime;
        }

        if (period.StartsWith(MonthlyPrefixPeriod))
        {
            return TimeSpan.FromDays(30);
        }

        if (period.StartsWith(MidnightPrefixPeriod))
        {
            return TimeSpan.Zero;
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
            //A sequence of decimal numbers each with an optional fraction and unit suffix
            //Valid time units are: 'ns', 'us'/'µs', 'ms', 's', 'm', and 'h'
            double totalMilliseconds = 0;
            var durationString = period.Split(' ').Last().Trim();
            
            foreach (Match  match in durationRegex.Matches(durationString))
            {
                var value = double.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
                var unit = match.Groups["unit"].Value.ToLower();

                totalMilliseconds += unit switch
                {
                    "ns" => value / 1_000_000,
                    "us" or "µs" => value / 1_000,
                    "ms" => value,
                    "s" => value * 1_000,
                    "m" => value * 1_000 * 60,
                    "h" => value * 1_000 * 60 * 60,
                    _ => throw new ArgumentException($"Unknown duration unit: {unit}")
                };
            }

            return TimeSpan.FromMilliseconds(totalMilliseconds);
        }
        
        throw new ArgumentException($"Unknown prefix period expression: {period}");
    }
}
