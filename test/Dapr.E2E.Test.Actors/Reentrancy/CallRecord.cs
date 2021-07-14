// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;

namespace Dapr.E2E.Test.Actors.Reentrancy
{
    public class CallRecord
    {
        public bool IsEnter { get; set; }
        public DateTime Timestamp { get; set; }

        public int CallNumber { get; set; }
    }
}
