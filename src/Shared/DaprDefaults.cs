// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System;

namespace Dapr
{
    internal static class DaprDefaults
    {
        private static string httpEndpoint;
        private static string grpcEndpoint;
        private static string daprApiToken;
        private static string appApiToken;

        /// <summary>
        /// Get the value of environment variable DAPR_API_TOKEN
        /// </summary>
        /// <returns>The value of environment variable DAPR_API_TOKEN</returns>
        public static string GetDefaultDaprApiToken()
        {
            // Lazy-init is safe because this is just populating the default
            // We don't plan to support the case where the user changes environment variables
            // for a running process.
            if (daprApiToken == null)
            {
                // Treat empty the same as null since it's an environment variable
                var value = Environment.GetEnvironmentVariable("DAPR_API_TOKEN");
                daprApiToken = (value == string.Empty) ? null : value;
            }

            return daprApiToken;
        }

        /// <summary>
        /// Get the value of environment variable APP_API_TOKEN
        /// </summary>
        /// <returns>The value of environment variable APP_API_TOKEN</returns>
        public static string GetDefaultAppApiToken()
        {
            if (appApiToken == null)
            {
                var value = Environment.GetEnvironmentVariable("APP_API_TOKEN");
                appApiToken = (value == string.Empty) ? null : value;
            }

            return appApiToken;
        }

        /// <summary>
        /// Get the value of environment variable DAPR_HTTP_PORT
        /// </summary>
        /// <returns>The value of environment variable DAPR_HTTP_PORT</returns>
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

        /// <summary>
        /// Get the value of environment variable DAPR_GRPC_PORT
        /// </summary>
        /// <returns>The value of environment variable DAPR_GRPC_PORT</returns>
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
