using System;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow;

internal static partial class Logging
{
    [LoggerMessage(LogLevel.Information, "Scheduled workflow '{WorkflowName}' with instance ID '{InstanceId}'")]
    public static partial void LogScheduleWorkflowSuccess(this ILogger logger, string workflowName, string instanceId);
    
    [LoggerMessage(LogLevel.Error, "Workflow instance '{InstanceId}' not found")]
    public static partial void LogGetWorkflowMetadataInstanceNotFound(this ILogger logger, string instanceId);
    
    [LoggerMessage(LogLevel.Error, "Workflow instance '{InstanceId}' not found")]
    public static partial void LogGetWorkflowMetadataInstanceNotFound(this ILogger logger, RpcException ex, string instanceId);
    
    [LoggerMessage(LogLevel.Error, "Workflow instance '{instanceId}' does not exist")]
    public static partial void LogWaitForStartException(this ILogger logger, InvalidOperationException ex,
        string instanceId);

    [LoggerMessage(LogLevel.Debug, "Workflow '{InstanceId}' started with status '{Status}'")]
    public static partial void LogWaitForStartCompleted(this ILogger logger, string instanceId, WorkflowRuntimeStatus status);
    
    [LoggerMessage(LogLevel.Error, "Workflow instance '{InstanceId}' does not exist")]
    public static partial void LogWaitForCompletionException(this ILogger logger, InvalidOperationException ex, string instanceId);

    [LoggerMessage(LogLevel.Debug, "Workflow '{InstanceId}' completed with status '{Status}'")]
    public static partial void LogWaitForCompletionCompleted(this ILogger logger, string instanceId, WorkflowRuntimeStatus status);
    
    [LoggerMessage(LogLevel.Information, "Raised event '{EventName}' to workflow '{InstanceId}'")]
    public static partial void LogRaisedEvent(this ILogger logger, string eventName, string instanceId)
    
    [LoggerMessage(LogLevel.Information, "Terminated workflow '{InstanceId}'")]
    public static partial void LogTerminateWorkflow(this ILogger logger, string instanceId);
    
    [LoggerMessage(LogLevel.Information, "Suspended workflow '{InstanceId}'")]
    public static partial void LogSuspendWorkflow(this ILogger logger, string instanceId);
    
    [LoggerMessage(LogLevel.Information, "Resumed workflow '{InstanceId}'")]
    public static partial void LogResumedWorkflow(this ILogger logger, string instanceId);
    
    [LoggerMessage(LogLevel.Information, "Purged workflow: '{InstanceId}'")]
    public static partial void LogPurgedWorkflowSuccessfully(this ILogger logger, string instanceId);

    [LoggerMessage(LogLevel.Debug, "Workflow '{InstanceId}' was not purged (may not exist or not in terminal state)")]
    public static partial void LogPurgedWorkflowUnsuccessfully(this ILogger logger, string instanceId);

    /// <summary>
    /// Creates a logger named "Dapr.Workflow" with an optional subcategory.
    /// </summary>
    /// <returns></returns>
    internal static ILogger CreateSubLogger(ILoggerFactory loggerFactory, string? subcategory = null)
    {
        string categoryName = "Dapr.Workflow";
        if (!string.IsNullOrEmpty(subcategory))
            categoryName += '.' + subcategory;

        return loggerFactory.CreateLogger(categoryName);
    }
}
