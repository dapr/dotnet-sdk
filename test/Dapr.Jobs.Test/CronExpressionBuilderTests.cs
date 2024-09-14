using System;
using Xunit;
using ArgumentException = System.ArgumentException;
using DayOfWeek = Dapr.Jobs.DayOfWeek;

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
            .Every(EveryCronPeriod.Month, 3);
        var result = builder.ToString();
        Assert.Equal("*/10 */8 */2 */5 */3 *", result);
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
}
