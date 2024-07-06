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

namespace Dapr.Jobs;

/// <summary>
/// Provides extension methods used with <see cref="TimeSpan"/>.
/// </summary>
internal static class TimeSpanExtensions
{
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
            sb.Append($"{timespan.Hours}h");

        if (timespan.Minutes > 0)
            sb.Append($"{timespan.Minutes}m");

        if (timespan.Seconds > 0)
            sb.Append($"{timespan.Seconds}s");

        if (timespan.Milliseconds > 0)
            sb.Append($"{timespan.Milliseconds}ms");

        return sb.ToString();
    }
}
