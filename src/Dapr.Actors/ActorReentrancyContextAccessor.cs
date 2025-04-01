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

using System.Threading;

namespace Dapr.Actors;

/// <summary>
/// Accessor for the reentrancy context. This provides the necessary ID to continue a reentrant request
/// across actor invocations.
/// </summary>
internal static class ActorReentrancyContextAccessor
{
    private static readonly AsyncLocal<ActorReentrancyContextHolder> state = new AsyncLocal<ActorReentrancyContextHolder>();

    /// <summary>
    /// The reentrancy context for a given request, if one is present.
    /// </summary>
    public static string ReentrancyContext
    {
        get
        {
            return state.Value?.Context;
        }
        set
        {
            var holder = state.Value;
            // Reset the current state if it exists.
            if (holder != null)
            {
                holder.Context = null;
            }

            if (value != null)
            {
                state.Value = new ActorReentrancyContextHolder { Context = value };
            }
        }
    }

    private class ActorReentrancyContextHolder
    {
        public string Context;
    }
}