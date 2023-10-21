// ------------------------------------------------------------------------
// Copyright 2022 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Workflow
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Defines runtime options for workflows.
    /// </summary>
    internal sealed class WorkflowLoggingService : IHostedService
    {
        private readonly ILogger<WorkflowLoggingService> logger;
        private static HashSet<string>? registeredWorkflows;
        private static HashSet<string>? registeredActivities;
        private LogLevel logLevel = LogLevel.Debug;

        public WorkflowLoggingService(ILogger<WorkflowLoggingService> logger)
        {
            var value = Environment.GetEnvironmentVariable("DAPR_LOG_LEVEL");
            logLevel = string.IsNullOrEmpty(value) ? LogLevel.Debug : (LogLevel)Enum.Parse(typeof(LogLevel), value);
            this.logger = logger;
            registeredActivities = new HashSet<string>();
            registeredWorkflows = new HashSet<string>();
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Log(logLevel, "WorkflowLoggingService started");
    
            if (registeredWorkflows != null)
            {
                this.logger.Log(logLevel, "List of registered workflows");
                foreach (string item in registeredWorkflows)
                {
                    this.logger.Log(logLevel, item);
                }
            }

            if (registeredActivities != null)
            {
                this.logger.Log(logLevel, "List of registered activities:");
                foreach (string item in registeredActivities)
                {
                    this.logger.Log(logLevel, item);
                }
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Log(logLevel, "WorkflowLoggingService stopped");
    
            return Task.CompletedTask;
        }

        public static void LogWorkflowName(string workflowName)
        {
            if (registeredWorkflows != null)
            {
                registeredWorkflows.Add(workflowName);
            }
        }

        public static void LogActivityName(string activityName)
        {
            if (registeredActivities != null)
            {
                registeredActivities.Add(activityName);
            }
        }
    }
}
