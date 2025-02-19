// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

using System.Text;
using System.Text.RegularExpressions;

namespace Dapr.Jobs;

/// <summary>
/// Provides extension methods used with <see cref="TimeSpan"/>.
/// </summary>
internal static class TimeSpanExtensions
{
    private static readonly Regex hourRegex = new Regex(@"(\d+)h", RegexOptions.Compiled);
    private static readonly Regex minuteRegex = new Regex(@"(\d+)m", RegexOptions.Compiled);
    private static readonly Regex secondRegex = new Regex(@"(\d+)s", RegexOptions.Compiled);
    private static readonly Regex millisecondRegex = new Regex(@"(\d+)q", RegexOptions.Compiled);

    /// <summary>
    /// Creates a duration string that matches the specification at https://pkg.go.dev/time#ParseDuration per the
    /// Jobs API specification https://v1-14.docs.dapr.io/reference/api/jobs_api/#schedule-a-job.
    /// </summary>
    /// <param name="timespan">The timespan being evaluated.</param>
    /// <returns></returns>
    public static string ToDurationString(this TimeSpan timespan)
    {
        var sb = new StringBuilder();

        //Hours is the largest unit of measure in the duration string
        if (timespan.Hours > 0)
        {
            sb.Append($"{timespan.Hours}h");
        }

        if (timespan.Minutes > 0)
        {
            sb.Append($"{timespan.Minutes}m");
        }

        if (timespan.Seconds > 0)
        {
            sb.Append($"{timespan.Seconds}s");
        }

        if (timespan.Milliseconds > 0)
        {
            sb.Append($"{timespan.Milliseconds}ms");
        }

        return sb.ToString();
    }

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
    /// Creates a <see cref="TimeSpan"/> given a Golang duration string.
    /// </summary>
    /// <param name="interval">The duration string to parse.</param>
    /// <returns>A timespan value.</returns>
    public static TimeSpan FromDurationString(this string interval)
    {
        interval = interval.Replace("ms", "q");

        int hours = 0;
        int minutes = 0;
        int seconds = 0;
        int milliseconds = 0;

        var hourMatch = hourRegex.Match(interval);
        if (hourMatch.Success)
        {
            hours = int.Parse(hourMatch.Groups[1].Value);
        }

        var minuteMatch = minuteRegex.Match(interval);
        if (minuteMatch.Success)
        {
            minutes = int.Parse(minuteMatch.Groups[1].Value);
        }

        var secondMatch = secondRegex.Match(interval);
        if (secondMatch.Success)
        {
            seconds = int.Parse(secondMatch.Groups[1].Value);
        }

        var millisecondMatch = millisecondRegex.Match(interval);
        if (millisecondMatch.Success)
        {
            milliseconds = int.Parse(millisecondMatch.Groups[1].Value);
        }

        return new TimeSpan(0, hours, minutes, seconds, milliseconds);
    }
}
