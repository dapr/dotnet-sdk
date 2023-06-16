// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

namespace Dapr.Client
{
    /// <summary>
    /// Represents workflow failure details.
    /// </summary>
    /// <param name="ErrorMessage">A summary description of the failure, which is typically an exception message.</param>
    /// <param name="ErrorType">The error type, which is defined by the workflow component implementation.</param>
    /// <param name="StackTrace">The stack trace of the failure.</param>
    public record WorkflowFailureDetails(
        string ErrorMessage,
        string ErrorType,
        string StackTrace = null)
    {
        /// <summary>
        /// Creates a user-friendly string representation of the failure information.
        /// </summary>
        public override string ToString()
        {
            return $"{this.ErrorType}: {this.ErrorMessage}";
        }
    }
}
