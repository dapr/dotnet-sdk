// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
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