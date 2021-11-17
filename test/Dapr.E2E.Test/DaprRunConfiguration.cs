// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.E2E.Test
{
    public class DaprRunConfiguration
    {
        public bool UseAppPort { get; set; }

        public string AppId { get; set; }

        public string AppProtocol { get; set; }

        public string ConfigurationPath { get; set; }

        public string TargetProject { get; set; }
    }
}