using System;
using Dapr.DurableTask.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow;

internal static partial class Logging
{
    [LoggerMessage(LogLevel.Information, "Starting Dapr Workflow Worker")]
    public static partial void LogWorkerWorkflowStart(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Stopping Dapr Workflow Worker")]
    public static partial void LogWorkerWorkflowStop(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "Workflow worker stopped")]
    public static partial void LogWorkerWorkflowCanceled(this ILogger logger);

    [LoggerMessage(LogLevel.Error, "Fatal error in workflow worker")]
    public static partial void LogWorkerWorkflowError(this ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Debug, "Executing workflow: Instance='{InstanceId}'")]
    public static partial void LogWorkerWorkflowHandleOrchestratorRequestStart(this ILogger logger, string? instanceId);
    
    [LoggerMessage(LogLevel.Error, "Workflow '{WorkflowName}' not found in registry")]
    public static partial void LogWorkerWorkflowHandleOrchestratorRequestNotInRegistry(this ILogger logger, string workflowName);
    
    [LoggerMessage(LogLevel.Information, "Workflow execution completed: Name='{WorkflowName}', InstanceId='{InstanceId}'")]
    public static partial void LogWorkerWorkflowHandleOrchestratorRequestCompleted(this ILogger logger, string workflowName, string instanceId);
    
    [LoggerMessage(LogLevel.Error, "Error executing workflow instance '{InstanceId}'")]
    public static partial void LogWorkerWorkflowHandleOrchestratorRequestFailed(this ILogger logger, Exception ex, string instanceId);

    [LoggerMessage(LogLevel.Debug, "Executing activity: Name='{ActivityName}', Instance='{InstanceId}', TaskId='{TaskId}'")]
    public static partial void LogWorkerWorkflowHandleActivityRequestStart(this ILogger logger, string activityName, string? instanceId, int taskId);
    
    [LoggerMessage(LogLevel.Error, "Activity '{ActivityName}' not found in registry")]
    public static partial void LogWorkerWorkflowHandleActivityRequestNotInRegistry(this ILogger logger, string activityName);
    
    [LoggerMessage(LogLevel.Debug, "Activity execution completed: Name='{ActivityName}', TaskId='{TasKId}'")]
    public static partial void LogWorkerWorkflowHandleActivityRequestCompleted(this ILogger logger, string activityName, int taskId);
    
    [LoggerMessage(LogLevel.Error, "Error executing activity '{ActivityName}' for instance '{InstanceId}'")]
    public static partial void LogWorkerWorkflowHandleActivityRequestFailed(this ILogger logger, Exception ex, string activityName, string? instanceId);
    
    [LoggerMessage(LogLevel.Warning, "Received unknown work item type: '{WorkItemType}'")]
    public static partial void LogGrpcProtocolHandlerUnknownWorkItemType(this ILogger logger, WorkItem.RequestOneofCase workItemType);
    
    [LoggerMessage(LogLevel.Debug, "Processing workflow request: Instance='{InstanceId}', Active={activeCount}")]
    public static partial void LogGrpcProtocolHandlerWorkflowProcessorStart(this ILogger logger, string instanceId, int activeCount);
    
    [LoggerMessage(LogLevel.Information, "Workflow processing canceled: '{InstanceId}'")]
    public static partial void LogGrpcProtocolHandlerWorkflowProcessorCanceled(this ILogger logger, string instanceId);
    
    [LoggerMessage(LogLevel.Error, "Error processing workflow for instance '{InstanceId}'")]
    public static partial void LogGrpcProtocolHandlerWorkflowProcessorError(this ILogger logger, Exception ex, string? instanceId);
    
    [LoggerMessage(LogLevel.Error, "Failed to send workflow failure result for '{InstanceId}'")]
    public static partial void LogGrpcProtocolHandlerWorkflowProcessorFailedToSendError(this ILogger logger, Exception ex, string instanceId);
    
    [LoggerMessage(LogLevel.Debug, "Processing activity request: Instance='{InstanceId}', Activity='{ActivityName}', TaskId='{TaskId}', Active={activeCount}")]
    public static partial void LogGrpcProtocolHandlerActivityProcessorStart(this ILogger logger, string instanceId, string activityName, int taskId, int activeCount);
    
    [LoggerMessage(LogLevel.Information, "Activity processing canceled: '{ActivityName}'")]
    public static partial void LogGrpcProtocolHandlerActivityProcessorCanceled(this ILogger logger, string activityName);
    
    [LoggerMessage(LogLevel.Error, "Error processing activity '{ActivityName}' for instance '{InstanceId}'")]
    public static partial void LogGrpcProtocolHandlerActivityProcessorError(this ILogger logger, Exception ex, string activityName, string? instanceId);
    
    [LoggerMessage(LogLevel.Error, "Failed to send activity failure result for '{ActivityName}'")]
    public static partial void LogGrpcProtocolHandlerActivityProcessorFailedToSendError(this ILogger logger, Exception ex, string activityName);
    
    [LoggerMessage(LogLevel.Information, "Receive loop completed, waiting for {Count} active work items")]
    public static partial void LogGrpcProtocolHandlerReceiveLoopCompleted(this ILogger logger, int count);
    
    [LoggerMessage(LogLevel.Error, "Error in receive loop")]
    public static partial void LogGrpcProtocolHandlerReceiveLoopError(this ILogger logger, Exception ex);

    [LoggerMessage(LogLevel.Information, "Disposing gRPC protocol handler")]
    public static partial void LogGrpcProtocolHandlerDisposing(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "gRPC protocol handler disposed")]
    public static partial void LogGrpcProtocolHandlerDisposed(this ILogger logger);
    
    [LoggerMessage(LogLevel.Information, "Starting gRPC bidirectional stream with Dapr sidecar")]
    public static partial void LogGrpcProtocolHandlerStartStream(this ILogger logger);

    [LoggerMessage(LogLevel.Information, "gRPC stream canceled")]
    public static partial void LogGrpcProtocolHandlerStreamCanceled(this ILogger logger);

    [LoggerMessage(LogLevel.Error, "Error in gRPC protocol handler")]
    public static partial void LogGrpcProtocolHandlerGenericError(this ILogger logger, Exception ex);
    
    [LoggerMessage(LogLevel.Debug, "Successfully created activity instance for '{ActivityName}'")]
    public static partial void LogCreateActivityInstanceSuccess(this ILogger logger, string activityName);

    [LoggerMessage(LogLevel.Warning, "Activity '{ActivityName}' not found in registry")]
    public static partial void LogCreateActivityNotFoundInRegistry(this ILogger logger, string activityName);

    [LoggerMessage(LogLevel.Error, "Failed to create activity instance for '{ActivityName}'")]
    public static partial void LogCreateActivityFailure(this ILogger logger, Exception ex, string activityName);
    
    [LoggerMessage(LogLevel.Debug, "Successfully created workflow instance for '{WorkflowName}'")]
    public static partial void LogCreateWorkflowInstanceSuccess(this ILogger logger, string workflowName);
    
    [LoggerMessage(LogLevel.Warning, "Workflow '{WorkflowName}' not found in registry")]
    public static partial void LogCreateWorkflowNotFoundInRegistry(this ILogger logger, string workflowName);
    
    [LoggerMessage(LogLevel.Error, "Failed to create workflow instance for '{WorkflowName}'")]
    public static partial void LogCreateWorkflowFailure(this ILogger logger, Exception ex, string workflowName);
    
    [LoggerMessage(LogLevel.Debug, "Registered activity '{ActivityName}'")]
    public static partial void LogRegisterActivitySuccess(this ILogger logger, string activityName);

    [LoggerMessage(LogLevel.Warning, "Activity '{ActivityName}' is already registered")]
    public static partial void LogRegisterActivityAlreadyRegistered(this ILogger logger, string activityName);
    
    [LoggerMessage(LogLevel.Debug, "Registered workflow '{WorkflowName}'")]
    public static partial void LogRegisterWorkflowSuccess(this ILogger logger, string workflowName);

    [LoggerMessage(LogLevel.Warning, "Workflow '{WorkflowName}' is already registered")]
    public static partial void LogRegisterWorkflowAlreadyRegistered(this ILogger logger, string workflowName);
    
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
    public static partial void LogRaisedEvent(this ILogger logger, string eventName, string instanceId);
    
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
    
    [LoggerMessage(LogLevel.Debug, "Scheduling activity '{ActivityName}' for workflow '{InstanceId}'")]
    public static partial void LogSchedulingActivity(this ILogger logger, string activityName, string instanceId);
    
    [LoggerMessage(LogLevel.Debug, "Activity '{ActivityName}' completed from history for workflow '{InstanceId}'")]
    public static partial void LogActivityCompletedFromHistory(this ILogger logger, string activityName, string instanceId);
    
    [LoggerMessage(LogLevel.Debug, "Activity '{ActivityName}' failed from history for workflow '{InstanceId}'")]
    public static partial void LogActivityFailedFromHistory(this ILogger logger, string activityName, string instanceId);
    
    [LoggerMessage(LogLevel.Debug, "Scheduling timer to fire at '{FireAt}' for workflow '{InstanceId}'")]
    public static partial void LogSchedulingTimer(this ILogger logger, DateTime fireAt, string instanceId);
    
    [LoggerMessage(LogLevel.Debug, "Timer fired from history for workflow '{InstanceId}'")]
    public static partial void LogTimerFiredFromHistory(this ILogger logger, string instanceId);
    
    [LoggerMessage(LogLevel.Debug, "Scheduling child workflow '{WorkflowName}' with instance '{ChildInstanceId}' for parent '{ParentInstanceId}'")]
    public static partial void LogSchedulingChildWorkflow(this ILogger logger, string workflowName, string childInstanceId, string parentInstanceId);
    
    [LoggerMessage(LogLevel.Debug, "Child workflow '{WorkflowName}' completed from history for parent '{ParentInstanceId}'")]
    public static partial void LogChildWorkflowCompletedFromHistory(this ILogger logger, string workflowName, string parentInstanceId);
    
    [LoggerMessage(LogLevel.Debug, "Child workflow '{WorkflowName}' failed from history for parent '{ParentInstanceId}'")]
    public static partial void LogChildWorkflowFailedFromHistory(this ILogger logger, string workflowName, string parentInstanceId);
}
