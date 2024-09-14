using System.Runtime.Serialization;
using Dapr.Common;
using ArgumentOutOfRangeException = System.ArgumentOutOfRangeException;

namespace Dapr.Jobs.Models
{


    /// <summary>
    /// 
    /// </summary>
    public sealed class CronExpressionBuilder
    {
        private string _seconds = "*";
        private string _minutes = "*";
        private string _hours = "*";
        private string _dayOfMonth = "*";
        private string _month = "*";
        private string _dayOfWeek = "*";

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
                    throw new ArgumentOutOfRangeException(nameof(values), "All values must be within 1 and 31, inclusively."); }

            var strValue = string.Join(',', values.Distinct().OrderBy(a => a));

            switch (period)
            {
                case OnCronPeriod.Second:
                    _seconds = strValue;
                    break;
                case OnCronPeriod.Minute:
                    _minutes = strValue;
                    break;
                case OnCronPeriod.Hour:
                    _hours = strValue;
                    break;
                case OnCronPeriod.DayOfMonth:
                    _dayOfMonth = strValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(period), period, null);
            }

            return this;
        }

        /// <summary>
        /// Reflects an expression in which the developer specifies a series of months in the year on which the trigger should occur.
        /// </summary>
        /// <param name="months">The months of the year to invoke the trigger on.</param>
        public CronExpressionBuilder On(params MonthOfYear[] months)
        {
            _month = string.Join(',', months.Distinct().OrderBy(a => a));
            return this;
        }

        /// <summary>
        /// Reflects an expression in which the developer specifies a series of days of the week on which the trigger should occur.
        /// </summary>
        /// <param name="days">The days of the week to invoke the trigger on.</param>
        public CronExpressionBuilder On(params DayOfWeek[] days)
        {
            _dayOfWeek = string.Join(',', days.Distinct().OrderBy(a => a).Select(a => a.GetValueFromEnumMember()));
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
                    _seconds = "*";
                    break;
                case CronPeriod.Minute:
                    _minutes = "*";
                    break;
                case CronPeriod.Hour:
                    _hours = "*";
                    break;
                case CronPeriod.DayOfMonth:
                    _dayOfMonth = "*";
                    break;
                case CronPeriod.Month:
                    _month = "*";
                    break;
                case CronPeriod.DayOfWeek:
                    _dayOfWeek = "*";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(period), period, null);
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
                    _seconds = value;
                    break;
                case EveryCronPeriod.Minute:
                    _minutes = value;
                    break;
                case EveryCronPeriod.Hour:
                    _hours = value;
                    break;
                case EveryCronPeriod.Month:
                    _month = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(period), period, null);
            }

            return this;
        }

        /// <summary>
        /// Builds the Cron expression.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{_seconds} {_minutes} {_hours} {_dayOfMonth} {_month} {_dayOfWeek}";
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
        [EnumMember(Value="SUN")]
        Sunday = 0,
        /// <summary>
        /// Monday.
        /// </summary>
        [EnumMember(Value="MON")]
        Monday = 1,
        /// <summary>
        /// Tuesday.
        /// </summary>
        [EnumMember(Value="TUE")]
        Tuesday = 2,
        /// <summary>
        /// Wednesday.
        /// </summary>
        [EnumMember(Value="WED")]
        Wednesday = 3,
        /// <summary>
        /// Thursday.
        /// </summary>
        [EnumMember(Value="THU")]
        Thursday = 4,
        /// <summary>
        /// Friday.
        /// </summary>
        [EnumMember(Value="FRI")]
        Friday = 5,
        /// <summary>
        /// Saturday.
        /// </summary>
        [EnumMember(Value="SAT")]
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
        January = 1,
        /// <summary>
        /// Month of February.
        /// </summary>
        February = 2,
        /// <summary>
        /// Month of March.
        /// </summary>
        March = 3,
        /// <summary>
        /// Month of April.
        /// </summary>
        April = 4,
        /// <summary>
        /// Month of May.
        /// </summary>
        May = 5,
        /// <summary>
        /// Month of June.
        /// </summary>
        June = 6,
        /// <summary>
        /// Month of July.
        /// </summary>
        July = 7,
        /// <summary>
        /// Month of August.
        /// </summary>
        August = 8,
        /// <summary>
        /// Month of September.
        /// </summary>
        September = 9,
        /// <summary>
        /// Month of October.
        /// </summary>
        October = 10,
        /// <summary>
        /// Month of November.
        /// </summary>
        November = 11,
        /// <summary>
        /// Month of December.
        /// </summary>
        December = 12
    }
}
