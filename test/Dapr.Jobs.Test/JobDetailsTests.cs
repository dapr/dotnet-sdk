using System;
using Dapr.Jobs.Models.Responses;
using Xunit;

namespace Dapr.Jobs.Test;

public class JobDetailsTests
{
    [Fact]
    public void JobDetails_CronExpressionShouldPopulateFromSchedule()
    {
        const string cronSchedule = "5 4 * * *";

        var jobDetails = new JobDetails { Schedule = cronSchedule };

        Assert.False(jobDetails.IsIntervalExpression);
        Assert.True(jobDetails.IsCronExpression);
        Assert.Null(jobDetails.Interval);
        Assert.Equal(cronSchedule, jobDetails.Schedule);
        Assert.Equal(cronSchedule, jobDetails.CronExpression);
    }

    [Fact]
    public void JobDetails_IntervalShouldPopulateFromSchedule()
    {
        var interval = new TimeSpan(4, 2, 1);
        var intervalString = interval.ToDurationString();

        var jobDetails = new JobDetails { Schedule = intervalString };

        Assert.True(jobDetails.IsIntervalExpression);
        Assert.False(jobDetails.IsCronExpression);
        Assert.Null(jobDetails.CronExpression);
        Assert.Equal(intervalString, jobDetails.Schedule);
        Assert.Equal(interval, jobDetails.Interval);
    }
}
