// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    using System.Threading;

    internal static class ActorLogicalCallContext
    {
        private static readonly AsyncLocal<string> fabActAsyncLocal = new AsyncLocal<string>();

        public static bool IsPresent()
        {
            return (fabActAsyncLocal.Value != null);
        }

        public static bool TryGet(out string callContextValue)
        {
            callContextValue = fabActAsyncLocal.Value;
            return (callContextValue != null);
        }

        public static void Set(string callContextValue)
        {
            fabActAsyncLocal.Value = callContextValue;
        }

        public static void Clear()
        {
            fabActAsyncLocal.Value = null;
        }
    }
}
