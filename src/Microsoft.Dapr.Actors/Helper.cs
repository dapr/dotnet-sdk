// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
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
