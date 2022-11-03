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

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.DurableTask;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Defines properties and methods for task activity context objects.
    /// </summary>
    public class ActivityContext
    {
        readonly TaskActivityContext innerContext;

        internal ActivityContext(TaskActivityContext innerContext)
        {
            this.innerContext = innerContext ?? throw new ArgumentNullException(nameof(innerContext));
        }

        /// <summary>
        /// Gets the name of the activity.
        /// </summary>
        public TaskName Name => this.innerContext.Name;

        /// <summary>
        /// Gets the unique ID of the current workflow instance.
        /// </summary>
        public string InstanceId => this.innerContext.InstanceId;
    }
}