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
    public void EveryFiveSeconds()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 5);
        var result = builder.ToString();
        Assert.Equal("5 * * * * *", result);
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
    public void EveryHour()
    {
        var builder = new CronExpressionBuilder()
            .On(OnCronPeriod.Second, 0)
            .On(OnCronPeriod.Minute, 0);
        var result = builder.ToString();
        Assert.Equal("0 0 * * * *", result);
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
}
