using Microsoft.DurableTask;

namespace Dapr.Workflow
{
    /// <summary>
    /// Defines properties and methods for workflow metadata.
    /// </summary>
    public class WorkflowMetadata
    {
        internal WorkflowMetadata(OrchestrationMetadata? metadata)
        {
            this.Details = metadata;
        }
        /// <summary>
        /// Method to check if workflow exists.
        /// </summary>
        public bool Exists => this.Details != null;
        /// <summary>
        /// Method to check if workflow is running.
        /// </summary>
        public bool IsWorkflowRunning => this.Details?.RuntimeStatus == OrchestrationRuntimeStatus.Running; 
        /// <summary>
        /// Method to get the workflow details.
        /// </summary>
        public OrchestrationMetadata? Details { get; }
    }
}