// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors
{
    using System;
    using System.Globalization;
    using Dapr.Actors.Communication;
    using Dapr.Actors.Reentrancy;

    internal class Helper
    {
        public static string GetCallContext()
        {
            if (ActorLogicalCallContext.TryGet(out var callContextValue))
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}{1}",
                    callContextValue,
                    Guid.NewGuid().ToString());
            }
            else
            {
                return Guid.NewGuid().ToString();
            }
        }

        public static string GetReentrancyContext()
        {
            if (ActorLogicalReentrancyContext.TryGet(out var reentrancyContext))
            {
                return reentrancyContext;
            }
            return string.Empty;
        }

        public static void SetReentrancyContext(string reentrancyId)
        {
            ActorLogicalReentrancyContext.Set(reentrancyId);
        }
    }
}
