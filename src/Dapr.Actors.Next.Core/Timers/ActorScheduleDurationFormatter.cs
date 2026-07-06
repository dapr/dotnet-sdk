using System.Globalization;

namespace Dapr.Actors.Next.Core.Timers;

internal static class ActorScheduleDurationFormatter
{
    internal static string Format(TimeSpan duration)
    {
        if (duration == TimeSpan.Zero)
        {
            return "0ms";
        }

        return duration.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture) + "ms";
    }
}
