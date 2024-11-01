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
using Xunit;
using ArgumentException = System.ArgumentException;

namespace Dapr.Jobs.Test;

public sealed class CronExpressionBuilderTests
{
    [Fact]
    public void WildcardByDefault()
    {
        var builder = new CronExpressionBuilder();
        var result = builder.ToString();
        Assert.Equal("* * * * * *", result);
    }

    [Fact]
    public void WildcardByAssertion()
    {
        var builder = new CronExpressionBuilder()
            .Each(CronPeriod.Second)
            .Each(CronPeriod.Minute)
            .Each(CronPeriod.Hour)
            .Each(CronPeriod.DayOfWeek)
            .Each(CronPeriod.DayOfMonth)
            .Each(CronPeriod.Month);
        var result = builder.ToString();
        Assert.Equal("* * * * * *", result);
    }

    [Fact]
    public void OnVariations()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 5)
            .On(OnCronPeriod.Minute, 12)
            .On(OnCronPeriod.Hour, 16)
            .On(OnCronPeriod.DayOfMonth, 7);
        var result = builder.ToString();
        Assert.Equal("5 12 16 7 * *", result);
    }

    [Fact]
    public void BottomOfEveryMinute()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 30);
        var result = builder.ToString();
        Assert.Equal("30 * * * * *", result);
    }

    [Fact]
    public void EveryFiveSeconds()
    {
        var builder = new CronExpressionBuilder()
            .Every(EveryCronPeriod.Second, 5);
        var result = builder.ToString();
        Assert.Equal("*/5 * * * * *", result);
    }

    [Fact]
    public void BottomOfEveryHour()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 0)
            .On(OnCronPeriod.Minute, 30);
        var result = builder.ToString();
        Assert.Equal("0 30 * * * *", result);
    }

    [Fact]
    public void EveryTwelveMinutes()
    {
        var builder = new CronExpressionBuilder()
            .Every(EveryCronPeriod.Minute, 12);
        var result = builder.ToString();
        Assert.Equal("* */12 * * * *", result);
    }

    [Fact]
    public void EveryHour()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 0)
            .On(OnCronPeriod.Minute, 0);
        var result = builder.ToString();
        Assert.Equal("0 0 * * * *", result);
    }

    [Fact]
    public void EveryFourHours()
    {
        var builder = new CronExpressionBuilder()
            .Every(EveryCronPeriod.Hour, 4);
        var result = builder.ToString();
        Assert.Equal("* * */4 * * *", result);
    }

    [Fact]
    public void EveryOtherMonth()
    {
        var builder = new CronExpressionBuilder()
            .Every(EveryCronPeriod.Month, 2);
        var result = builder.ToString();
        Assert.Equal("* * * * */2 *", result);
    }

    [Fact]
    public void EachMonth()
    {
        var builder = new CronExpressionBuilder()
            .Every(EveryCronPeriod.Month, 4)
            .Each(CronPeriod.Month);
        var result = builder.ToString();
        Assert.Equal("* * * * * *", result);
    }

    [Fact]
    public void EveryDayAtMidnight()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 0)
            .On(OnCronPeriod.Minute, 0)
            .On(OnCronPeriod.Hour, 0);
        var result = builder.ToString();
        Assert.Equal("0 0 0 * * *", result);
    }

    [Fact]
    public void EveryFourthDayInJanAprAugAndDecIfTheDayIsWednesdayOrFriday()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 30)
            .On(OnCronPeriod.Minute, 15)
            .On(OnCronPeriod.Hour, 6)
            .Every(EveryCronPeriod.DayInMonth, 4)
            .On(MonthOfYear.January, MonthOfYear.April, MonthOfYear.August, MonthOfYear.December)
            .On(DayOfWeek.Wednesday, DayOfWeek.Friday);
        var result = builder.ToString();
        Assert.Equal("30 15 6 */4 JAN,APR,AUG,DEC WED,FRI", result);
    }

    [Fact]
    public void EveryValidation()
    {
        var builder = new CronExpressionBuilder()
            .Every(EveryCronPeriod.Second, 10)
            .Every(EveryCronPeriod.Minute, 8)
            .Every(EveryCronPeriod.Hour, 2)
            .Every(EveryCronPeriod.DayInMonth, 5)
            .Every(EveryCronPeriod.DayInWeek, 2)
            .Every(EveryCronPeriod.Month, 3);
        var result = builder.ToString();
        Assert.Equal("*/10 */8 */2 */5 */3 */2", result);
    }

    [Fact]
    public void EveryDayAtNoon()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 0)
            .On(OnCronPeriod.Minute, 0)
            .On(OnCronPeriod.Hour, 12);
        var result = builder.ToString();
        Assert.Equal("0 0 12 * * *", result);
    }

    [Fact]
    public void MidnightOnTuesdaysAndFridays()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 0)
            .On(OnCronPeriod.Minute, 0)
            .On(OnCronPeriod.Hour, 0)
            .On(DayOfWeek.Tuesday, DayOfWeek.Friday);
        var result = builder.ToString();
        Assert.Equal("0 0 0 * * TUE,FRI", result);
    }

    [Fact]
    public void FourThirtyPmOnWednesdayThroughSaturdayFromOctoberToDecember()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 0)
            .On(OnCronPeriod.Minute, 30)
            .On(OnCronPeriod.Hour, 16)
            .Through(DayOfWeek.Wednesday, DayOfWeek.Saturday)
            .Through(MonthOfYear.October, MonthOfYear.December);
        var result = builder.ToString();
        Assert.Equal("0 30 16 * OCT-DEC WED-SAT", result);
    }

    [Fact]
    public void ThroughFirstAvailableUnits()
    {
        var builder = new CronExpressionBuilder()
            .Through(ThroughCronPeriod.Second, 0, 15)
            .Through(ThroughCronPeriod.Minute, 0, 15)
            .Through(ThroughCronPeriod.Hour, 0, 15)
            .Through(ThroughCronPeriod.DayOfMonth, 1, 10)
            .Through(ThroughCronPeriod.Month, 0, 8);
        var result = builder.ToString();
        Assert.Equal("0-15 0-15 0-15 1-10 0-8 *", result);
    }

    [Fact]
    public void ShouldThrowIfIntervalIsBelowRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .Every(EveryCronPeriod.Minute, -5);
        });
    }

    [Fact]
    public void ShouldThrowIfRangeValuesAreEqual()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .Through(ThroughCronPeriod.Hour, 8, 8);
        });
    }

    [Fact]
    public void ShouldThrowIfRangeValuesAreInDescendingOrder()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .Through(MonthOfYear.December, MonthOfYear.February);
        });
    }

    [Fact]
    public void ShouldThrowIfRangedMonthsAreEqual()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .Through(MonthOfYear.April, MonthOfYear.April);
        });
    }

    [Fact]
    public void ShouldThrowIfRangedMonthsAreInDescendingOrder()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .Through(ThroughCronPeriod.Minute, 10, 5);
        });
    }

    [Fact]
    public void ShouldThrowIfRangedDaysAreEqualInRange()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .Through(DayOfWeek.Thursday, DayOfWeek.Thursday);
        });
    }

    [Fact]
    public void ShouldThrowIfRangedDaysAreInDescendingOrder()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .Through(DayOfWeek.Thursday, DayOfWeek.Monday);
        });
    }

    [Fact]
    public void ShouldThrowIfOnValuesAreBelowRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .On(OnCronPeriod.Second, -2);
        });
    }

    [Fact]
    public void ShouldThrowIfOnValuesAreBelowRange2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .On(OnCronPeriod.Hour, -10);
        });
    }

    [Fact]
    public void ShouldThrowIfOnValuesAreBelowRange3()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .On(OnCronPeriod.DayOfMonth, -5);
        });
    }

    [Fact]
    public void ShouldThrowIfOnValuesAreAboveRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .On(OnCronPeriod.Minute, 60);
        });
    }

    [Fact]
    public void ShouldThrowIfOnValuesAreAboveRange2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            var builder = new CronExpressionBuilder()
                .On(OnCronPeriod.DayOfMonth, 32);
        });
    }

    [Theory]
    [InlineData("* * * * *", false)]
    [InlineData("* * * * * *", true)]
    [InlineData("5 12 16 7 * *", true)]
    [InlineData("30 * * * * *", true)]
    [InlineData("*/5 * * * * *", true)]
    [InlineData("0 30 * * * *", true)]
    [InlineData("* */12 * * * *", true)]
    [InlineData("0 0 * * * *", true)]
    [InlineData("* * */4 * * *", true)]
    [InlineData("* * * * */2 *", true)]
    [InlineData("0 0 0 * * *", true)]
    [InlineData("30 15 6 */4 JAN,APR,AUG WED,FRI", true)]
    [InlineData("*/10 */8 */2 */5 */3 *", true)]
    [InlineData("0 0 12 * * *", true)]
    [InlineData("0 0 0 * * TUE,FRI", true)]
    [InlineData("0 0 0 * * TUE", true)]
    [InlineData("0 0 0 * * TUE-FRI", true)]
    [InlineData("0 30 16 * OCT SAT", true)]
    [InlineData("0 30 16 * OCT,DEC WED,SAT", true)]
    [InlineData("0 30 16 * OCT-DEC WED-SAT", true)]
    [InlineData("0-15 * * * * *", true)]
    [InlineData("0-15 02-59 * * * *", true)]
    [InlineData("0-15 02-59 07-23 * * *", true)]
    [InlineData("0-15 0-15 0-15 1-10 8-16 *", true)]
    [InlineData("5 12 16 7 FEB *", true)]
    [InlineData("5 12 16 7 * MON", true)]
    [InlineData("5 12 16 7 JAN SAT", true)]
    [InlineData("5 * * * FEB SUN", true)]
    [InlineData("* * */2 * * *", true)]
    [InlineData("* * * */5 * *", true)]
    [InlineData("0,01,3 0,01,2 0,01,2 00,1,02 JAN,FEB,MAR,APR SUN,MON,TUE,WED", true)]
    [InlineData("* * * * JAN,FEB,MAR,APR,MAY,JUN,JUL,AUG,SEP,OCT,NOV,DEC SUN,MON,TUE,WED,THU,FRI,SAT", true)]
    [InlineData("30 15 6 */4 JAN,APR,AUG WED-FRI", true)]
    [InlineData("*/10 */8 */2 */5 */3 */2", true)]
    [InlineData("0 0 0 * OCT SAT", true)]
    [InlineData("0 0 0 * OCT,DEC WED,SAT", true)]
    [InlineData("0 0 0 * OCT-DEC WED-SAT", true)]
    [InlineData("1-14 2-59 20-23 * * *", true)]
    [InlineData("00-59 0-59 00-23 1-31 JAN-DEC SUN-SAT", true)]
    [InlineData("0-59 0-59 0-23 1-31 1-12 0-6", true)]
    [InlineData("*/1 2,4,5 * 2-9 JAN,FEB,DEC MON-WED", true)]
    public void ValidateCronExpression(string cronValue, bool isValid)
    {
        var result = CronExpressionBuilder.IsCronExpression(cronValue);
        Assert.Equal(result, isValid);
    }
}
