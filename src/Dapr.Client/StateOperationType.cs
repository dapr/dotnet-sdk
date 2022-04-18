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
    /// Operation type for state operations with Dapr.
    /// </summary>
    public enum StateOperationType
    {
        /// <summary>
        /// Upsert a new or existing state
        /// </summary>
        Upsert,

        /// <summary>
        /// Delete a state
        /// </summary>
        Delete,
    }
}
