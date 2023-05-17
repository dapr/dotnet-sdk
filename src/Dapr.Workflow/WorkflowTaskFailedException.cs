// ------------------------------------------------------------------------
// Copyright 2023 The Dapr Authors
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

    /// <summary>
    /// Exception type for Dapr Workflow task failures.
    /// </summary>
    public class WorkflowTaskFailedException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowTaskFailedException"/> class.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="failureDetails">Details about the failure.</param>
        public WorkflowTaskFailedException(string message, WorkflowTaskFailureDetails failureDetails)
            : base(message)
        {
            this.FailureDetails = failureDetails ?? throw new ArgumentNullException(nameof(failureDetails));
        }

        /// <summary>
        /// Gets more information about the underlying workflow task failure.
        /// </summary>
        public WorkflowTaskFailureDetails FailureDetails { get; }
    }
}
