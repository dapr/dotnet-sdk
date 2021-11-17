// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;

namespace Dapr
{
    internal static class DaprDefaults
    {
        private static string httpEndpoint;
        private static string grpcEndpoint;
        private static string apiToken;

        public static string GetDefaultApiToken()
        {
            // Lazy-init is safe because this is just populating the default
            // We don't plan to support the case where the user changes environment variables
            // for a running process.
            if (apiToken == null)
            {
                // Treat empty the same as null since it's an environment variable
                var value = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");
                apiToken = (value == string.Empty) ? null : value;
            }

            return apiToken;

            
        }

        public static string GetDefaultHttpEndpoint()
        {
            if (httpEndpoint == null)
            {
                var port = Environment.GetEnvironmentVariable("DAPR_HTTP_PORT");
                port = string.IsNullOrEmpty(port) ? "3500" : port;
                httpEndpoint = $"http://127.0.0.1:{port}";
            }

            return httpEndpoint;
        }

        public static string GetDefaultGrpcEndpoint()
        {
            if (grpcEndpoint == null)
            {
                var port = Environment.GetEnvironmentVariable("DAPR_GRPC_PORT");
                port = string.IsNullOrEmpty(port) ? "50001" : port;
                grpcEndpoint = $"http://127.0.0.1:{port}";
            }

            return grpcEndpoint;
        }
    }
}
