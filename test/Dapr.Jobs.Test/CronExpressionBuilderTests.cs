using Dapr.Jobs.Models;
using Xunit;

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
    public void FourThirtyPmOnWednesdayThroughSaturday()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 0)
            .On(OnCronPeriod.Minute, 30)
            .On(OnCronPeriod.Hour, 16)
            .Through(DayOfWeek.Wednesday, DayOfWeek.Saturday);
        var result = builder.ToString();
        Assert.Equal("0 30 16 * * WED-SAT", result);
    }
}
