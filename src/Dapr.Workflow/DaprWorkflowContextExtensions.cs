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

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See LICENSE at
// https://github.com/Azure/azure-functions-durable-extension/blob/dev/LICENSE for license information.

using Microsoft.Extensions.Logging;

namespace Dapr.Workflow;

/// <summary>
/// Defines convenient overloads for calling context methods.
/// </summary>
public static class DaprWorkflowContextExtensions
{
    /// <summary>
    /// Returns an instance of <see cref="ILogger"/> that is replay safe, ensuring the logger logs only
    /// when the orchestrator is not replaying that line of code.
    /// </summary>
    /// <param name="context">The workflow context.</param>
    /// <param name="logger">An instance of <see cref="ILogger"/>.</param>
    /// <returns>An instance of a replay-safe <see cref="ILogger"/>.</returns>
    public static ILogger CreateReplaySafeLogger(this IWorkflowContext context, ILogger logger) =>
        new ReplaySafeLogger(logger, context);
}
