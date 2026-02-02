// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

namespace Dapr.Workflow;

/// <summary>
/// Provides functionality available to orchestration code.
/// </summary>
public interface IWorkflowContext
{
    /// <summary>
    /// Gets a value indicating whether the orchestration or operation is currently replaying itself.
    /// </summary>
    /// <remarks>
    /// This property is useful when there is logic that needs to run only when *not* replaying. For example,
    /// certain types of application logging may become too noisy when duplicated as part of replay. The
    /// application code could check to see whether the function is being replayed and then issue
    /// the log statements when this value is <c>false</c>.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the orchestration or operation is currently being replayed; otherwise <c>false</c>.
    /// </value>
    bool IsReplaying { get; }
}
