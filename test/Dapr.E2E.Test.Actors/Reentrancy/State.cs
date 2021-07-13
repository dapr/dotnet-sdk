// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Collections.Generic;

namespace Dapr.E2E.Test.Actors.Reentrancy
{
    public class State
    {
        public List<CallRecord> Records { get; set; } = new List<CallRecord>();
    }
}
