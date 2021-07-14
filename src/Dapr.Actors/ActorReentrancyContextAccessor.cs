// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Threading;

namespace Dapr.Actors
{
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
}
