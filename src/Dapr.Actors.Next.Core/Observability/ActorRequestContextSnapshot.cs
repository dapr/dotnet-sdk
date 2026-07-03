using System.Diagnostics;
using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Core.Observability;

/// <summary>
/// Captures and restores process-local actor request context.
/// </summary>
public static class ActorRequestContextSnapshot
{
    private static readonly ActivitySource ActivitySource = new("Dapr.Actors.Next.Core.Context");
    private static readonly IReadOnlyDictionary<string, string> EmptyBaggage =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));
    private static readonly ActorRequestContext EmptyContext = new(null, null, EmptyBaggage);

    /// <summary>
    /// Captures the current ambient activity and baggage.
    /// </summary>
    public static ActorRequestContext Capture()
    {
        var activity = Activity.Current;
        if (activity is null)
        {
            return EmptyContext;
        }

        Dictionary<string, string>? baggage = null;
        foreach (var item in activity.Baggage)
        {
            baggage ??= new Dictionary<string, string>(StringComparer.Ordinal);
            baggage[item.Key] = item.Value ?? string.Empty;
        }

        return new ActorRequestContext(activity.Id, activity.TraceStateString, baggage ?? EmptyBaggage);
    }

    /// <summary>
    /// Restores baggage from a captured request context for the lifetime of the returned scope.
    /// </summary>
    public static IDisposable Restore(ActorRequestContext context)
    {
        if (!ShouldRestore(context))
        {
            return NullRestoreScope.Instance;
        }

        var parent = CreateActivityContext(context);
        var activity = ActivitySource.StartActivity("actor.context", ActivityKind.Internal, parent);
        if (activity is not null)
        {
            foreach (var pair in context.Baggage)
            {
                activity.AddBaggage(pair.Key, pair.Value);
            }
        }

        return new RestoreScope(activity);
    }

    internal static bool ShouldRestore(ActorRequestContext context) =>
        ActivitySource.HasListeners()
        && (!string.IsNullOrEmpty(context.TraceParent)
            || !string.IsNullOrEmpty(context.TraceState)
            || context.Baggage.Count > 0);

    internal static ActivityContext CreateActivityContext(ActorRequestContext context)
    {
        if (string.IsNullOrEmpty(context.TraceParent))
        {
            return default;
        }

        return ActivityContext.TryParse(context.TraceParent, context.TraceState, out var activityContext)
            ? activityContext
            : default;
    }

    private sealed class NullRestoreScope : IDisposable
    {
        public static readonly NullRestoreScope Instance = new();

        public void Dispose()
        {
        }
    }

    private sealed class RestoreScope : IDisposable
    {
        private readonly Activity? activity;

        public RestoreScope(Activity? activity)
        {
            this.activity = activity;
        }

        public void Dispose()
        {
            activity?.Dispose();
        }
    }
}
