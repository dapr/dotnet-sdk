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

using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using Dapr.Common;
using ArgumentException = System.ArgumentException;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;

namespace Dapr.Jobs;

/// <summary>
/// A fluent API used to build a valid Cron expression.
/// </summary>
public sealed class CronExpressionBuilder
{
    private static readonly Regex secondsAndMinutesRegex = new(@"^\*|(\*|(\*\/([0-5]?\d))|(([0-5]?\d)(-([0-5]?\d))?))$", RegexOptions.Compiled);
    private static readonly Regex hoursRegex = new(@"^\*|(\*\/(([0-1]?\d)|(2[0-3])))|((([0-1]?\d)|(2[0-3]))(-(([0-1]?\d)|(2[0-3])))?)$", RegexOptions.Compiled);
    private static readonly Regex dayOfTheMonthRegex = new(@"^\*|(\*\/(([0-2]?\d)|(3[0-1])))|(((([0-2]?\d)|(3[0-1]))(-(([0-2]?\d)|(3[0-1])))?))$", RegexOptions.Compiled);
    private static readonly Regex monthRegex = new(@"^\*|(\*\/([0-1]?\d))|((([0-1]?\d)(-([0-1]?\d))?))|((,?(JAN|DEC|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC)+)+)|(-?(JAN|DEC|FEB|MAR|APR|MAY|JUN|JUL|AUG|SEP|OCT|NOV|DEC))$");
    private static readonly Regex dayOfTheWeekRegex = new(@"^\*|(\*\/(0?[0-6])|(0?[0-6](-0?[0-6])?)|((,?(SUN|MON|TUE|WED|THU|FRI|SAT))+)|((SUN|MON|TUE|WED|THU|FRI|SAT)(-(SUN|MON|TUE|WED|THU|FRI|SAT))?))$", RegexOptions.Compiled);
    
    private string seconds = "*";
    private string minutes = "*";
    private string hours = "*";
    private string dayOfMonth = "*";
    private string month = "*";
    private string dayOfWeek = "*";

    /// <summary>
    /// Reflects an expression in which the developer specifies a series of numeric values and the period they're associated
    /// with indicating when the trigger should occur.
    /// </summary>
    /// <param name="period">The period of time within which the values should be associated.</param>
    /// <param name="values">The numerical values of the time period on which the schedule should trigger.</param>
    /// <returns></returns>
    public CronExpressionBuilder On(OnCronPeriod period, params int[] values)
    {
        switch (period)
        {
            //Validate by period
            case OnCronPeriod.Second or OnCronPeriod.Minute or OnCronPeriod.Hour when values.Any(a => a is < 0 or > 59):
                throw new ArgumentOutOfRangeException(nameof(values), "All values must be within 0 and 59, inclusively.");
            case OnCronPeriod.DayOfMonth when values.Any(a => a is < 0 or > 31):
                throw new ArgumentOutOfRangeException(nameof(values), "All values must be within 1 and 31, inclusively.");
        }

        var strValue = string.Join(',', values.Distinct().OrderBy(a => a));

        switch (period)
        {
            case OnCronPeriod.Second:
                seconds = strValue;
                break;
            case OnCronPeriod.Minute:
                minutes = strValue;
                break;
            case OnCronPeriod.Hour:
                hours = strValue;
                break;
            case OnCronPeriod.DayOfMonth:
                dayOfMonth = strValue;
                break;
        }

        return this;
    }

    /// <summary>
    /// Reflects an expression in which the developer specifies a series of months in the year on which the trigger should occur.
    /// </summary>
    /// <param name="months">The months of the year to invoke the trigger on.</param>
    public CronExpressionBuilder On(params MonthOfYear[] months)
    {
        month = string.Join(',', months.Distinct().OrderBy(a => a).Select(a => a.GetValueFromEnumMember()));
        return this;
    }

    /// <summary>
    /// Reflects an expression in which the developer specifies a series of days of the week on which the trigger should occur.
    /// </summary>
    /// <param name="days">The days of the week to invoke the trigger on.</param>
    public CronExpressionBuilder On(params DayOfWeek[] days)
    {
        dayOfWeek = string.Join(',', days.Distinct().OrderBy(a => a).Select(a => a.GetValueFromEnumMember()));
        return this;
    }

    /// <summary>
    /// Reflects an expression in which the developer defines bounded range of numerical values for the specified period.
    /// </summary>
    /// <param name="period">The period of time within which the values should be associated.</param>
    /// <param name="from">The start of the range.</param>
    /// <param name="to">The end of the range.</param>
    public CronExpressionBuilder Through(ThroughCronPeriod period, int from, int to)
    {
        if (from > to)
            throw new ArgumentException("The date representing the From property should precede the To property");
        if (from == to)
            throw new ArgumentException("The From and To properties should not be equivalent");

        var stringValue = $"{from}-{to}";

        switch (period)
        {
            case ThroughCronPeriod.Second:
                seconds = stringValue;
                break;
            case ThroughCronPeriod.Minute:
                minutes = stringValue;
                break;
            case ThroughCronPeriod.Hour:
                hours = stringValue;
                break;
            case ThroughCronPeriod.DayOfMonth:
                dayOfMonth = stringValue;
                break;
            case ThroughCronPeriod.Month:
                month = stringValue;
                break;
        }

        return this;
    }

    /// <summary>
    /// Reflects an expression in which the developer defines a bounded range of days.
    /// </summary>
    /// <param name="from">The start of the range.</param>
    /// <param name="to">The end of the range.</param>
    public CronExpressionBuilder Through(DayOfWeek from, DayOfWeek to)
    {
        if (from > to)
            throw new ArgumentException("The day representing the From property should precede the To property");
        if (from == to)
            throw new ArgumentException("The From and To properties should not be equivalent");

        dayOfWeek = $"{from.GetValueFromEnumMember()}-{to.GetValueFromEnumMember()}";
        return this;
    }

    /// <summary>
    /// Reflects an expression in which the developer defines a bounded range of months.
    /// </summary>
    /// <param name="from">The start of the range.</param>
    /// <param name="to">The end of the range.</param>
    public CronExpressionBuilder Through(MonthOfYear from, MonthOfYear to)
    {
        if (from > to)
            throw new ArgumentException("The month representing the From property should precede the To property");
        if (from == to)
            throw new ArgumentException("The From and To properties should not be equivalent");

        month = $"{from.GetValueFromEnumMember()}-{to.GetValueFromEnumMember()}";
        return this;
    }

    /// <summary>
    /// Reflects an expression in which the trigger should happen each time the value of the specified period changes.
    /// </summary>
    /// <param name="period">The period of time that should be evaluated.</param>
    /// <returns></returns>
    public CronExpressionBuilder Each(CronPeriod period)
    {
        switch (period)
        {
            case CronPeriod.Second:
                seconds = "*";
                break;
            case CronPeriod.Minute:
                minutes = "*";
                break;
            case CronPeriod.Hour:
                hours = "*";
                break;
            case CronPeriod.DayOfMonth:
                dayOfMonth = "*";
                break;
            case CronPeriod.Month:
                month = "*";
                break;
            case CronPeriod.DayOfWeek:
                dayOfWeek = "*";
                break;
        }

        return this;
    }

    /// <summary>
    /// Reflects an expression in which the trigger should happen at a regular interval of the specified period type.
    /// </summary>
    /// <param name="period">The length of time represented in a unit interval.</param>
    /// <param name="interval">The number of period units that should elapse between each trigger.</param>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    public CronExpressionBuilder Every(EveryCronPeriod period, int interval)
    {
        if (interval < 0)
            throw new ArgumentOutOfRangeException(nameof(interval));

        var value = $"*/{interval}";

        switch (period)
        {
            case EveryCronPeriod.Second:
                seconds = value;
                break;
            case EveryCronPeriod.Minute:
                minutes = value;
                break;
            case EveryCronPeriod.Hour:
                hours = value;
                break;
            case EveryCronPeriod.Month:
                month = value;
                break;
            case EveryCronPeriod.DayInMonth:
                dayOfMonth = value;
                break;
            case EveryCronPeriod.DayInWeek:
                dayOfWeek = value;
                break;
        }

        return this;
    }

    /// <summary>
    /// Validates whether a given expression is valid Cron syntax.
    /// </summary>
    /// <param name="expression">The string to evaluate.</param>
    /// <returns>True if the expression is valid Cron syntax; false if not.</returns>
    internal static bool IsCronExpression(string expression)
    {
        var subExpressions = expression.Split(' ');
        if (subExpressions.Length != 6)
            return false;

        //if (!secondsAndMinutesRegex.IsMatch(subExpressions[0]))
        //    return false;

        //if (!secondsAndMinutesRegex.IsMatch(subExpressions[1]))
        //    return false;

        //if (!hoursRegex.IsMatch(subExpressions[2]))
        //    return false;

        //if (!dayOfTheMonthRegex.IsMatch(subExpressions[3]))
        //    return false;

        //if (!monthRegex.IsMatch(subExpressions[4]))
        //    return false;

        //if (!dayOfTheWeekRegex.IsMatch(subExpressions[5]))
        //    return false;

        //return true;

        return secondsAndMinutesRegex.IsMatch(subExpressions[0])
               && secondsAndMinutesRegex.IsMatch(subExpressions[1])
               && hoursRegex.IsMatch(subExpressions[2])
               && dayOfTheMonthRegex.IsMatch(subExpressions[3])
               && monthRegex.IsMatch(subExpressions[4])
               && dayOfTheWeekRegex.IsMatch(subExpressions[5]);
    }

    /// <summary>
    /// Builds the Cron expression.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => $"{seconds} {minutes} {hours} {dayOfMonth} {month} {dayOfWeek}";
}

/// <summary>
/// Identifies the valid Cron periods in an "On" expression.
/// </summary>
public enum OnCronPeriod
{
    /// <summary>
    /// Identifies the second value for an "On" expression.
    /// </summary>
    Second,
    /// <summary>
    /// Identifies the minute value for an "On" expression.
    /// </summary>
    Minute,
    /// <summary>
    /// Identifies the hour value for an "On" expression.
    /// </summary>
    Hour,
    /// <summary>
    /// Identifies the day in the month for an "On" expression.
    /// </summary>
    DayOfMonth
}

/// <summary>
/// Identifies the valid Cron periods in an "Every" expression.
/// </summary>
public enum EveryCronPeriod
{
    /// <summary>
    /// Identifies the second value in an "Every" expression.
    /// </summary>
    Second,
    /// <summary>
    /// Identifies the minute value in an "Every" expression.
    /// </summary>
    Minute,
    /// <summary>
    /// Identifies the hour value in an "Every" expression.
    /// </summary>
    Hour,
    /// <summary>
    /// Identifies the month value in an "Every" expression.
    /// </summary>
    Month,
    /// <summary>
    /// Identifies the days in the month value in an "Every" expression.
    /// </summary>
    DayInMonth,
    /// <summary>
    /// Identifies the days in the week value in an "Every" expression.
    /// </summary>
    DayInWeek,
}

/// <summary>
/// Identifies the various Cron periods valid to use in a "Through" expression.
/// </summary>
public enum ThroughCronPeriod
{
    /// <summary>
    /// Identifies the second value in the Cron expression.
    /// </summary>
    Second,
    /// <summary>
    /// Identifies the minute value in the Cron expression.
    /// </summary>
    Minute,
    /// <summary>
    /// Identifies the hour value in the Cron expression.
    /// </summary>
    Hour,
    /// <summary>
    /// Identifies the day of month value in the Cron expression.
    /// </summary>
    DayOfMonth,
    /// <summary>
    /// Identifies the month value in the Cron expression.
    /// </summary>
    Month
}

/// <summary>
/// Identifies the various Cron periods.
/// </summary>
public enum CronPeriod
{
    /// <summary>
    /// Identifies the second value in the Cron expression.
    /// </summary>
    Second,
    /// <summary>
    /// Identifies the minute value in the Cron expression.
    /// </summary>
    Minute,
    /// <summary>
    /// Identifies the hour value in the Cron expression.
    /// </summary>
    Hour,
    /// <summary>
    /// Identifies the day of month value in the Cron expression.
    /// </summary>
    DayOfMonth,
    /// <summary>
    /// Identifies the month value in the Cron expression.
    /// </summary>
    Month,
    /// <summary>
    /// Identifies the day of week value in the Cron expression.
    /// </summary>
    DayOfWeek
}

/// <summary>
/// Identifies the days in the week.
/// </summary>
public enum DayOfWeek
{
    /// <summary>
    /// Sunday.
    /// </summary>
    [EnumMember(Value = "SUN")]
    Sunday = 0,
    /// <summary>
    /// Monday.
    /// </summary>
    [EnumMember(Value = "MON")]
    Monday = 1,
    /// <summary>
    /// Tuesday.
    /// </summary>
    [EnumMember(Value = "TUE")]
    Tuesday = 2,
    /// <summary>
    /// Wednesday.
    /// </summary>
    [EnumMember(Value = "WED")]
    Wednesday = 3,
    /// <summary>
    /// Thursday.
    /// </summary>
    [EnumMember(Value = "THU")]
    Thursday = 4,
    /// <summary>
    /// Friday.
    /// </summary>
    [EnumMember(Value = "FRI")]
    Friday = 5,
    /// <summary>
    /// Saturday.
    /// </summary>
    [EnumMember(Value = "SAT")]
    Saturday = 6
}

/// <summary>
/// Identifies the months in the year.
/// </summary>
public enum MonthOfYear
{
    /// <summary>
    /// Month of January.
    /// </summary>
    [EnumMember(Value = "JAN")]
    January = 1,
    /// <summary>
    /// Month of February.
    /// </summary>
    [EnumMember(Value = "FEB")]
    February = 2,
    /// <summary>
    /// Month of March.
    /// </summary>
    [EnumMember(Value = "MAR")]
    March = 3,
    /// <summary>
    /// Month of April.
    /// </summary>
    [EnumMember(Value = "APR")]
    April = 4,
    /// <summary>
    /// Month of May.
    /// </summary>
    [EnumMember(Value = "MAY")]
    May = 5,
    /// <summary>
    /// Month of June.
    /// </summary>
    [EnumMember(Value = "JUN")]
    June = 6,
    /// <summary>
    /// Month of July.
    /// </summary>
    [EnumMember(Value = "JUL")]
    July = 7,
    /// <summary>
    /// Month of August.
    /// </summary>
    [EnumMember(Value = "AUG")]
    August = 8,
    /// <summary>
    /// Month of September.
    /// </summary>
    [EnumMember(Value = "SEP")]
    September = 9,
    /// <summary>
    /// Month of October.
    /// </summary>
    [EnumMember(Value = "OCT")]
    October = 10,
    /// <summary>
    /// Month of November.
    /// </summary>
    [EnumMember(Value = "NOV")]
    November = 11,
    /// <summary>
    /// Month of December.
    /// </summary>
    [EnumMember(Value = "DEC")]
    December = 12
}
