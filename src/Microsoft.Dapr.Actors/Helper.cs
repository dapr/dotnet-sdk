// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors
{
    using System;
    using System.Globalization;
    using Microsoft.Dapr.Actors.Communication;

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
    }
}
