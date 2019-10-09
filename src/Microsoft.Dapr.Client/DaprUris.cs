// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Microsoft.Dapr
{
    using System;

    internal static class DaprUris
    {
        public const string StatePath = "/v1.0/state";

        public static string DefaultPort => Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500";
    }
}