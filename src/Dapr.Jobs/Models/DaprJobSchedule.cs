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

using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dapr.Jobs.Extensions;
using Dapr.Jobs.JsonConverters;

namespace Dapr.Jobs.Models;

/// <summary>
/// Used to build a schedule for a job.
/// </summary>
[JsonConverter(typeof(DaprJobScheduleConverter))]
public sealed class DaprJobSchedule
{
    /// <summary>
    /// A regular expression used to evaluate whether a given prefix period embodies an @every statement.
    /// </summary>
    private static readonly Regex isEveryExpression = new(@"^@every (\d+(m?s|m|h))+$", RegexOptions.Compiled);
    /// <summary>
    /// The various prefixed period values allowed.
    /// </summary>
    private static readonly string[] acceptablePeriodValues = { "yearly", "monthly", "weekly", "daily", "midnight", "hourly" };

    /// <summary>
    /// The value of the expression represented by the schedule.
    /// </summary>
    public string ExpressionValue { get; }
    
    /// <summary>
    /// Initializes the value of <see cref="ExpressionValue"/> based on the provided value from each of the factory methods.
    /// </summary>
    /// <remarks>
    /// Developers are intended to create a new <see cref="DaprJobSchedule"/> using the provided static factory methods.
    /// </remarks>
    /// <param name="expressionValue">The value of the scheduling expression.</param>
    internal DaprJobSchedule(string expressionValue)
    {
        ExpressionValue = expressionValue;
    }

    /// <summary>
    /// Specifies a schedule built using the fluent Cron expression builder.
    /// </summary>
    /// <param name="builder">The fluent Cron expression builder.</param>
    public static DaprJobSchedule FromCronExpression(CronExpressionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder, nameof(builder));
        return new DaprJobSchedule(builder.ToString());
    }

    /// <summary>
    /// Specifies a single point in time.
    /// </summary>
    /// <param name="scheduledTime">The date and time when the job should be triggered.</param>
    /// <returns></returns>
    public static DaprJobSchedule FromDateTime(DateTimeOffset scheduledTime) => new(scheduledTime.ToString("O"));

    /// <summary>
    /// Specifies a schedule using a Cron-like expression or '@' prefixed period strings.
    /// </summary>
    /// <param name="expression">The systemd Cron-like expression indicating when the job should be triggered.</param>
    public static DaprJobSchedule FromExpression(string expression)
    {
#if NET6_0
        ArgumentNullException.ThrowIfNull(expression, nameof(expression));
#endif
        return new DaprJobSchedule(expression);
    }

    /// <summary>
    /// Specifies a schedule using a duration interval articulated via a <see cref="TimeSpan"/>.
    /// </summary>
    /// <param name="duration">The duration interval.</param>
    public static DaprJobSchedule FromDuration(TimeSpan duration) => new($"@every {duration.ToDurationString()}");

    /// <summary>
    /// Specifies a schedule in which the job is triggered to run once a year.
    /// </summary>
    public static DaprJobSchedule Yearly { get; } = new DaprJobSchedule("@yearly");

    /// <summary>
    /// Specifies a schedule in which the job is triggered monthly.
    /// </summary>
    public static DaprJobSchedule Monthly { get; } = new DaprJobSchedule("@monthly");

    /// <summary>
    /// Specifies a schedule in which the job is triggered weekly.
    /// </summary>
    public static DaprJobSchedule Weekly { get; } = new DaprJobSchedule("@weekly");

    /// <summary>
    /// Specifies a schedule in which the job is triggered daily.
    /// </summary>
    public static DaprJobSchedule Daily { get; } = new DaprJobSchedule("@daily");

    /// <summary>
    /// Specifies a schedule in which the job is triggered once a day at midnight.
    /// </summary>
    public static DaprJobSchedule Midnight { get; } = new DaprJobSchedule("@midnight");

    /// <summary>
    /// Specifies a schedule in which the job is triggered at the top of every hour.
    /// </summary>
    public static DaprJobSchedule Hourly { get; } = new DaprJobSchedule("@hourly");

    /// <summary>
    /// Reflects that the schedule represents a prefixed period expression.
    /// </summary>
    public bool IsPrefixedPeriodExpression =>
        ExpressionValue.StartsWith('@') &&
        (isEveryExpression.IsMatch(ExpressionValue) ||
         ExpressionValue.EndsWithAny(acceptablePeriodValues, StringComparison.InvariantCulture));

    /// <summary>
    /// Reflects that the schedule represents a fixed point in time.
    /// </summary>
    public bool IsPointInTimeExpression => DateTimeOffset.TryParse(ExpressionValue, out _);

    /// <summary>
    /// Reflects that the schedule represents a Golang duration expression.
    /// </summary>
    public bool IsDurationExpression => ExpressionValue.IsDurationString();

    /// <summary>
    /// Reflects that the schedule represents a Cron expression.
    /// </summary>
    public bool IsCronExpression => CronExpressionBuilder.IsCronExpression(ExpressionValue);
}
