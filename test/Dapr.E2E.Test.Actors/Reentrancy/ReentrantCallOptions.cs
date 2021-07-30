// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.E2E.Test.Actors.Reentrancy
{
    public class ReentrantCallOptions 
    {
        public int CallsRemaining { get; set; }

        public int CallNumber { get; set; }
    }
}