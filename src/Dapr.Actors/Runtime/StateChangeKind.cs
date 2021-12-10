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

namespace Dapr.Actors.Runtime
{
    /// <summary>
    /// Represents the kind of state change for an actor state when saves change is called to a set of actor states.
    /// </summary>
    public enum StateChangeKind
    {
        /// <summary>
        /// No change in state.
        /// </summary>
        None = 0,

        /// <summary>
        /// The state needs to be added.
        /// </summary>
        Add = 1,

        /// <summary>
        /// The state needs to be updated.
        /// </summary>
        Update = 2,

        /// <summary>
        /// The state needs to be removed.
        /// </summary>
        Remove = 3,
    }
}
