// ------------------------------------------------------------------------
//  Copyright 2026 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

namespace Dapr.Workflow.Versioning;

/// <summary>
/// Optional interface for strategies that want per-family context (canonical name and options scope).
/// </summary>
public interface IWorkflowVersionStrategyContextConsumer
{
    /// <summary>
    /// Configures the strategy with the canonical name and optional options scope.
    /// </summary>
    /// <param name="context">The strategy context.</param>
    void Configure(WorkflowVersionStrategyContext context);
}
