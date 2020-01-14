// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr
{
    using System;

    internal static class DaprUris
    {
        public const string StatePath = "/v1.0/state";
        public const string InvokePath = "/v1.0/invoke";
        public const string PublishPath = "/v1.0/publish";

        public static string DefaultHttpPort => Environment.GetEnvironmentVariable("DAPR_HTTP_PORT") ?? "3500";
    }
}