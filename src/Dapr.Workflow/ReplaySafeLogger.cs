// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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

// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License at
// https://github.com/microsoft/durabletask-dotnet/blob/main/LICENSE

using System;
using Microsoft.Extensions.Logging;

namespace Dapr.Workflow;

internal class ReplaySafeLogger : ILogger
{
    private readonly IWorkflowContext context;
    private readonly ILogger logger;

    public ReplaySafeLogger(IWorkflowContext context, ILogger logger)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    IDisposable ILogger.BeginScope<TState>(TState state)
    {
        ArgumentNullException.ThrowIfNull(state, nameof(state));
        return this.logger.BeginScope<TState>(state)!;
    }

    public bool IsEnabled(LogLevel logLevel) => this.logger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!this.context.IsReplaying)
        {
            this.logger.Log(logLevel, eventId, state, exception, formatter);
        }
    }
}
