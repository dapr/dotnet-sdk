// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Reentrancy
{
    using System;
    using System.Threading;

    internal static class ActorLogicalReentrancyContext
    {
        private static readonly AsyncLocal<string> fabActAsyncLocal = new AsyncLocal<string>();

        public static bool IsPresent()
        {
            return (fabActAsyncLocal.Value != null);
        }

        public static bool TryGet(out string reentrancyContextValue)
        {
            reentrancyContextValue = fabActAsyncLocal.Value;
            return (reentrancyContextValue != null);
        }

        public static void Set(string reentrancyContextValue)
        {
            fabActAsyncLocal.Value = reentrancyContextValue;
        }

        public static void Clear()
        {
            fabActAsyncLocal.Value = null;
        }
    }
}
