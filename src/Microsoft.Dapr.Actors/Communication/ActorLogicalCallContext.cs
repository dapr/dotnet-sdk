// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Communication
{
    using System.Threading;

    internal static class ActorLogicalCallContext
    {
        private static AsyncLocal<string> fabActAsyncLocal = new AsyncLocal<string>();

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
