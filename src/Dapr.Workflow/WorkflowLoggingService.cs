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
    using Microsoft.Extensions.Configuration;

    /// <summary>
    /// Defines runtime options for workflows.
    /// </summary>
    internal sealed class WorkflowLoggingService : IHostedService
    {
        private readonly ILogger<WorkflowLoggingService> logger;
        private static readonly HashSet<string> registeredWorkflows = new();
        private static readonly HashSet<string> registeredActivities = new();
        private LogLevel logLevel = LogLevel.Information;

        public WorkflowLoggingService(ILogger<WorkflowLoggingService> logger, IConfiguration configuration)
        {
            logLevel = string.IsNullOrEmpty(configuration["DAPR_LOG_LEVEL"]) ? LogLevel.Information : ConvertLogLevel(configuration["DAPR_LOG_LEVEL"]);
            this.logger = logger;
            
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.logger.Log(LogLevel.Information, "WorkflowLoggingService started");

                this.logger.Log(LogLevel.Information, "List of registered workflows");
                foreach (string item in registeredWorkflows)
                {
                    this.logger.Log(LogLevel.Information, item);
                }

                this.logger.Log(LogLevel.Information, "List of registered activities:");
                foreach (string item in registeredActivities)
                {
                    this.logger.Log(LogLevel.Information, item);
                }

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.logger.Log(LogLevel.Information, "WorkflowLoggingService stopped");
    
            return Task.CompletedTask;
        }

        public static void LogWorkflowName(string workflowName)
        {
            registeredWorkflows.Add(workflowName);
        }

        public static void LogActivityName(string activityName)
        {
            registeredActivities.Add(activityName);
        }

        private LogLevel ConvertLogLevel(string ?daprLogLevel)
        {
            LogLevel logLevel = LogLevel.Information; 
            if (!string.IsNullOrEmpty(daprLogLevel))
            {
                Enum.TryParse(daprLogLevel, out logLevel);
            }
            return logLevel;
        }
    }
}
