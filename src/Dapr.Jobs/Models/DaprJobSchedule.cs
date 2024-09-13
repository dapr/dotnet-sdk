namespace Dapr.Jobs;

/// <summary>
/// Used to build a schedule for a job.
/// </summary>
public sealed class DaprJobSchedule
{
    /// <summary>
    /// The value of the expression represented by the schedule.
    /// </summary>
    public string ExpressionValue { get; }

    /// <summary>
    /// Initializes the value of <see cref="ExpressionValue"/> based on the provided value from each of the factory methods.
    /// </summary>
    /// <param name="expressionValue">The value of the scheduling expression.</param>
    private DaprJobSchedule(string expressionValue)
    {
        ExpressionValue = expressionValue;
    }

    /// <summary>
    /// Specifies a schedule using a Cron-like expression or '@' prefixed period strings.
    /// </summary>
    /// <param name="expression">The systemd Cron-like expression indicating when the job should be triggered.</param>
    /// <returns></returns>
    public static DaprJobSchedule FromExpression(string expression)
    {
        ArgumentVerifier.ThrowIfNullOrEmpty(expression, nameof(expression));

        return new DaprJobSchedule(expression);
    }

    /// <summary>
    /// Specifies a schedule in which the job is triggered to run once a year.
    /// </summary>
    public static DaprJobSchedule Yearly => new DaprJobSchedule("@yearly");

    /// <summary>
    /// Specifies a schedule in which the job is triggered monthly.
    /// </summary>
    public static DaprJobSchedule Monthly => new DaprJobSchedule("@monthly");

    /// <summary>
    /// Specifies a schedule in which the job is triggered weekly.
    /// </summary>
    public static DaprJobSchedule Weekly => new DaprJobSchedule("@weekly");

    /// <summary>
    /// Specifies a schedule in which the job is triggered daily.
    /// </summary>
    public static DaprJobSchedule Daily => new DaprJobSchedule("@daily");

    /// <summary>
    /// Specifies a schedule in which the job is triggered once a day at midnight.
    /// </summary>
    public static DaprJobSchedule Midnight => new DaprJobSchedule("@midnight");

    /// <summary>
    /// Specifies a schedule in which the job is triggered at the top of every hour.
    /// </summary>
    public static DaprJobSchedule Hourly => new DaprJobSchedule("@hourly");
}
