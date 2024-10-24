namespace Dapr.Messaging.PublishSubscribe;

/// <summary>
/// Defines the policy for handling streaming message subscriptions, including retry logic and timeout settings.
/// </summary>
/// <param name="TimeoutDuration">The duration to wait before timing out a message handling operation.</param>
/// <param name="DefaultResponseAction">The default action to take when a message handling operation times out.</param>
public sealed record MessageHandlingPolicy(TimeSpan TimeoutDuration, TopicResponseAction DefaultResponseAction);

