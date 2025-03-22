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

using System;
using Dapr.Jobs.Models;
using Xunit;

namespace Dapr.Jobs.Test.Models;

public sealed class DaprJobScheduleTests
{
    [Fact]
    public void FromDuration_Validate()
    {
        var schedule = DaprJobSchedule.FromDuration(new TimeSpan(12, 8, 16));
        Assert.Equal("@every 12h8m16s", schedule.ExpressionValue);
    }

    [Fact]
    public void FromExpression_Duration()
    {
        var every5Seconds = new TimeSpan(0, 0, 0, 5);
        var schedule = DaprJobSchedule.FromDuration(every5Seconds);

        Assert.Equal("@every 5s", schedule.ExpressionValue);
    }

    [Fact]
    public void FromExpression_Cron()
    {
        const string cronExpression = "*/5 1-5 * * JAN,FEB WED-SAT";

        var schedule = DaprJobSchedule.FromExpression(cronExpression);
        Assert.Equal(cronExpression, schedule.ExpressionValue);
    }

    [Fact]
    public void FromExpression_PrefixedPeriod_Yearly()
    {
        var schedule = DaprJobSchedule.Yearly;
        Assert.Equal("@yearly", schedule.ExpressionValue);
    }

    [Fact]
    public void FromExpression_PrefixedPeriod_Monthly()
    {
        var schedule = DaprJobSchedule.Monthly;
        Assert.Equal("@monthly", schedule.ExpressionValue);
    }

    [Fact]
    public void FromExpression_PrefixedPeriod_Weekly()
    {
        var schedule = DaprJobSchedule.Weekly;
        Assert.Equal("@weekly", schedule.ExpressionValue);
    }

    [Fact]
    public void FromExpression_PrefixedPeriod_Daily()
    {
        var schedule = DaprJobSchedule.Daily;
        Assert.Equal("@daily", schedule.ExpressionValue);
    }

    [Fact]
    public void FromExpression_PrefixedPeriod_Midnight()
    {
        var schedule = DaprJobSchedule.Midnight;
        Assert.Equal("@midnight", schedule.ExpressionValue);
    }

    [Fact]
    public void FromExpression_PrefixedPeriod_Hourly()
    {
        var schedule = DaprJobSchedule.Hourly;
        Assert.Equal("@hourly", schedule.ExpressionValue);
    }

    [Fact]
    public void FromCronExpression()
    {
        var schedule = DaprJobSchedule.FromCronExpression(new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 15)
            .Every(EveryCronPeriod.Minute, 2)
            .Every(EveryCronPeriod.Hour, 4)
            .Through(ThroughCronPeriod.DayOfMonth, 2, 13)
            .Through(DayOfWeek.Monday, DayOfWeek.Saturday)
            .On(MonthOfYear.June, MonthOfYear.August, MonthOfYear.January));

        Assert.Equal("15 */2 */4 2-13 JAN,JUN,AUG MON-SAT", schedule.ExpressionValue);
    }

    [Fact]
    public void IsPointInTimeExpression()
    {
        var schedule = DaprJobSchedule.FromDateTime(DateTimeOffset.UtcNow.AddDays(2));
        Assert.True(schedule.IsPointInTimeExpression);
        Assert.False(schedule.IsDurationExpression);
        Assert.False(schedule.IsCronExpression);
        Assert.False(schedule.IsPrefixedPeriodExpression);
    }

    [Fact]
    public void IsDurationExpression()
    {
        var schedule = DaprJobSchedule.FromDuration(TimeSpan.FromHours(2));
        Assert.True(schedule.IsDurationExpression);
        Assert.False(schedule.IsPointInTimeExpression);
        Assert.False(schedule.IsCronExpression);
        Assert.True(schedule.IsPrefixedPeriodExpression); //A duration expression _is_ a prefixed period with @every
    }

    [Fact]
    public void IsPrefixedPeriodExpression()
    {
        var schedule = DaprJobSchedule.Weekly;
        Assert.True(schedule.IsPrefixedPeriodExpression);
        Assert.False(schedule.IsCronExpression);
        Assert.False(schedule.IsPointInTimeExpression);
        Assert.False(schedule.IsDurationExpression);
    }

    [Theory]
    [InlineData("5h")]
    [InlineData("5h5m")]
    [InlineData("5h2m12s")]
    [InlineData("5h9m22s27ms")]
    [InlineData("42m12s28ms")]
    [InlineData("19s2ms")]
    [InlineData("292ms")]
    [InlineData("5h23s")]
    [InlineData("25m192ms")]
    public void ValidateEveryExpression(string testValue)
    {
        var schedule = DaprJobSchedule.FromExpression($"@every {testValue}");
        Assert.True(schedule.IsPrefixedPeriodExpression);
    }

    [Theory]
    [InlineData("* * * * * *")]
    [InlineData("5 12 16 7 * *")]
    [InlineData("5 12 16 7 FEB *")]
    [InlineData("5 12 16 7 * MON")]
    [InlineData("5 12 16 7 JAN SAT")]
    [InlineData("5 * * * FEB SUN")]
    [InlineData("30 * * * * *")]
    [InlineData("*/5 * * * * *")]
    [InlineData("* */12 * * * *")]
    [InlineData("* * */2 * * *")]
    [InlineData("* * * */5 * *")]
    [InlineData("30 15 6 */4 JAN,APR,AUG WED-FRI")]
    [InlineData("*/10 */8 */2 */5 */3 */2")]
    [InlineData("0 0 0 * * TUE,FRI")]
    [InlineData("0 0 0 * * TUE-FRI")]
    [InlineData("0 0 0 * OCT SAT")]
    [InlineData("0 0 0 * OCT,DEC WED,SAT")]
    [InlineData("0 0 0 * OCT-DEC WED-SAT")]
    [InlineData("0-15 * * * * *")]
    [InlineData("0-15 02-59 * * * *")]
    [InlineData("1-14 2-59 20-23 * * *")]
    [InlineData("0-59 0-59 0-23 1-31 1-12 0-6")]
    [InlineData("*/1 2,4,5 * 2-9 JAN,FEB,DEC MON-WED")]
    public void IsCronExpression(string testValue)
    {
        var schedule = DaprJobSchedule.FromExpression(testValue);
        Assert.True(schedule.IsCronExpression);
        Assert.False(schedule.IsPrefixedPeriodExpression);
        Assert.False(schedule.IsPointInTimeExpression);
        Assert.False(schedule.IsDurationExpression);
    }
}
